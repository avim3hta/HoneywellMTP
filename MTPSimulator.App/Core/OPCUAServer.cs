using System;
using System.Globalization;
using System.Threading.Tasks;
using MTPSimulator.App.Models;
using MTPSimulator.App.Utils;
using System.Net;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

namespace MTPSimulator.App.Core
{
	public sealed class OPCUAServer
	{
		private MTPNode? _root;
		private ApplicationInstance? _application;
		private StandardServer? _server;
		private SimulatorNodeManager? _nodeManager;
		private readonly SimulationEngine _simulation;
		private readonly ValueStore _store = new();
		public string? BoundEndpointUrl { get; private set; }
		public event Action<string, object>? ValueChanged;

		public OPCUAServer()
		{
			_simulation = new SimulationEngine(ConfigManager.LoadOrDefault());
		}

		public void LoadNodes(MTPNode root)
		{
			_root = root;
			_simulation.Initialize(root);
		}

		public async Task StartAsync(string endpoint)
		{
			if (_server != null)
				return;

			var endpointUrl = NormalizeEndpoint(endpoint);

			var config = new ApplicationConfiguration
			{
				ApplicationName = "MTP OPC UA Simulator",
				ApplicationType = ApplicationType.Server,
				ApplicationUri = $"urn:{Dns.GetHostName()}:MTPSimulator",
				ProductUri = "urn:mtp-simulator",
				ServerConfiguration = new ServerConfiguration
				{
					BaseAddresses = { endpointUrl },
					MinRequestThreadCount = 2,
					MaxRequestThreadCount = 10
				},
				SecurityConfiguration = new SecurityConfiguration
				{
					ApplicationCertificate = new CertificateIdentifier
					{
						StoreType = CertificateStoreType.X509Store,
						StorePath = "CurrentUser\\My",
						SubjectName = "CN=MTP OPC UA Simulator"
					},
					AutoAcceptUntrustedCertificates = true,
					AddAppCertToTrustedStore = true,
					RejectSHA1SignedCertificates = false
				},
				TransportConfigurations = new TransportConfigurationCollection(),
				TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
				ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
				DisableHiResClock = true
			};

			await config.Validate(ApplicationType.Server).ConfigureAwait(false);

			_application = new ApplicationInstance
			{
				ApplicationName = config.ApplicationName,
				ApplicationType = ApplicationType.Server,
				ApplicationConfiguration = config
			};

			// Ensure a certificate exists even for None security
			await _application.CheckApplicationInstanceCertificate(false, 0).ConfigureAwait(false);

			var simulatorServer = new SimulatorServer(() => _root, nm => _nodeManager = nm);
			await _application.Start(simulatorServer).ConfigureAwait(false);
			_server = simulatorServer;
			BoundEndpointUrl = endpointUrl;

			// Start simulation and push values, honoring DB overrides
			if (_nodeManager != null)
			{
				_simulation.ValueUpdated += (nodeId, value) =>
				{
					try
					{
						// If a persisted override exists, use it instead of sim value
						var overrideVal = _store.TryGet(nodeId);
						if (overrideVal.found && overrideVal.value is string s && double.TryParse(s, out var dv))
						{
							_nodeManager.UpdateValue(nodeId, dv);
							ValueChanged?.Invoke(nodeId, dv);
						}
						else
						{
							_nodeManager.UpdateValue(nodeId, value);
							ValueChanged?.Invoke(nodeId, value);
						}
					}
					catch { }
				};
				_nodeManager.ExternalWrite += (nodeId, value) =>
				{
					try { _store.Upsert(nodeId, value); ValueChanged?.Invoke(nodeId, value); } catch { }
				};
			}
			_simulation.Start();
		}

		public void Stop()
		{
			_simulation.Stop();

			if (_server != null)
			{
				try { _server.Stop(); } catch { }
				_server = null;
			}
		}

		private static string NormalizeEndpoint(string input)
		{
			var trimmed = input.Trim();
			if (trimmed.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
				return trimmed;
			// allow forms like "127.0.0.1:4840" or "localhost:4840"
			return $"opc.tcp://{trimmed}";
		}

		public bool TryWriteValue(string nodeId, object value)
		{
			if (_nodeManager == null) return false;
			try
			{
				_nodeManager.UpdateValue(nodeId, value);
				_store.Upsert(nodeId, value);
				ValueChanged?.Invoke(nodeId, value);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	internal sealed class SimulatorServer : StandardServer
	{
		private readonly Func<MTPNode?> _rootProvider;
		private readonly Action<SimulatorNodeManager> _nodeManagerReady;

		public SimulatorServer(Func<MTPNode?> rootProvider, Action<SimulatorNodeManager> nodeManagerReady)
		{
			_rootProvider = rootProvider;
			_nodeManagerReady = nodeManagerReady;
		}

		protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
		{
			var nodeManagers = new INodeManager[]
			{
				new SimulatorNodeManager(server, configuration, _rootProvider)
			};
			var master = new MasterNodeManager(server, configuration, null, nodeManagers);
			_nodeManagerReady((SimulatorNodeManager)nodeManagers[0]);
			return master;
		}
	}

	internal sealed class SimulatorNodeManager : CustomNodeManager2
	{
		private readonly Func<MTPNode?> _rootProvider;
		private readonly Dictionary<string, BaseDataVariableState> _variables = new();
		public event Action<string, object>? ExternalWrite;

		public SimulatorNodeManager(IServerInternal server, ApplicationConfiguration configuration, Func<MTPNode?> rootProvider)
			: base(server, configuration, new[] { "urn:mtp-simulator:nodes" })
		{
			SystemContext.NodeIdFactory = this;
			_rootProvider = rootProvider;
		}

		public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
		{
			var root = _rootProvider();
			if (root == null)
				return;

			// Ensure a folder under Objects to host all nodes
			var objectsFolder = GetDefaultFolder(externalReferences);
			var mtpFolder = CreateFolder(objectsFolder, "MTP", "MTP");

			AddNodesFromMtp(root, mtpFolder);
		}

		private FolderState GetDefaultFolder(IDictionary<NodeId, IList<IReference>> externalReferences)
		{
			if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var refs))
			{
				refs = new List<IReference>();
				externalReferences[ObjectIds.ObjectsFolder] = refs;
			}

			return (FolderState)FindPredefinedNode(ObjectIds.ObjectsFolder, typeof(FolderState));
		}

		private FolderState CreateFolder(NodeState parent, string path, string name)
		{
			var folder = new FolderState(parent)
			{
				SymbolicName = name,
				ReferenceTypeId = ReferenceTypeIds.Organizes,
				TypeDefinitionId = ObjectTypeIds.FolderType,
				NodeId = new NodeId(path, NamespaceIndexes[0]),
				BrowseName = new QualifiedName(path, NamespaceIndexes[0]),
				DisplayName = name,
				WriteMask = 0,
				UserWriteMask = 0
			};

			if (parent == null)
			{
				AddPredefinedNode(SystemContext, folder);
			}
			else
			{
				parent.AddChild(folder);
			}

			return folder;
		}

		private void AddNodesFromMtp(MTPNode node, NodeState parent)
		{
			if (node.NodeClass == "Variable")
			{
				var varState = new BaseDataVariableState(parent)
				{
					BrowseName = new QualifiedName(node.BrowseName, NamespaceIndexes[0]),
					DisplayName = node.DisplayName,
					Description = null,
					NodeId = !string.IsNullOrWhiteSpace(node.NodeId) ? NodeId.Parse(node.NodeId) : null,
					DataType = GetDataTypeId(node.DataType),
					ValueRank = ValueRanks.Scalar,
					AccessLevel = AccessLevels.CurrentReadOrWrite,
					UserAccessLevel = AccessLevels.CurrentReadOrWrite,
					Historizing = false,
					Value = GetDefaultValue(node.DataType)
				};

				if (varState.NodeId == null)
				{
					varState.NodeId = new NodeId(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), NamespaceIndexes[0]);
				}

				parent.AddChild(varState);
				_variables[varState.NodeId.ToString()] = varState;

				// forward external writes to the UI/app
				varState.OnSimpleWriteValue = (ISystemContext context, NodeState nodeState, ref object value) =>
				{
					varState.Value = value;
					varState.Timestamp = DateTime.UtcNow;
					varState.ClearChangeMasks(SystemContext, false);
					ExternalWrite?.Invoke(varState.NodeId.ToString(), value);
					return ServiceResult.Good;
				};
				return;
			}

			// For folders/objects, create a container
			var folder = CreateFolder(parent, node.BrowseName, node.DisplayName);
			foreach (var child in node.Children)
			{
				AddNodesFromMtp(child, folder);
			}
		}

		private static NodeId GetDataTypeId(string? dt)
		{
			return dt switch
			{
				"Boolean" => DataTypeIds.Boolean,
				"Int32" => DataTypeIds.Int32,
				"Float" => DataTypeIds.Float,
				"Double" => DataTypeIds.Double,
				"String" => DataTypeIds.String,
				_ => DataTypeIds.Double
			};
		}

		private static object GetDefaultValue(string? dt)
		{
			return dt switch
			{
				"Boolean" => false,
				"Int32" => 0,
				"Float" => 0f,
				"Double" => 0d,
				"String" => string.Empty,
				_ => 0d
			};
		}

		public void UpdateValue(string nodeId, object value)
		{
			if (_variables.TryGetValue(nodeId, out var varState))
			{
				varState.Value = value;
				varState.Timestamp = DateTime.UtcNow;
				varState.ClearChangeMasks(SystemContext, false);
			}
		}
	}
}

