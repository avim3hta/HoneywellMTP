using System;
using System.Globalization;
using System.IO;
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
			
			// If server is already running, refresh the address space
			if (_nodeManager != null)
			{
				_nodeManager.RefreshAddressSpace();
			}
		}

		public async Task StartAsync(string endpoint)
		{
			if (_server != null)
				return;

			try
			{
				Console.WriteLine("Starting OPC UA Server...");
				var endpointUrl = NormalizeEndpoint(endpoint);
				Console.WriteLine($"Normalized endpoint: {endpointUrl}");

				Console.WriteLine("Creating application configuration...");
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

			Console.WriteLine("Validating configuration...");
			await config.Validate(ApplicationType.Server).ConfigureAwait(false);

			Console.WriteLine("Creating application instance...");
			_application = new ApplicationInstance
			{
				ApplicationName = config.ApplicationName,
				ApplicationType = ApplicationType.Server,
				ApplicationConfiguration = config
			};

			Console.WriteLine("Checking application certificate...");
			// Ensure a certificate exists even for None security
			await _application.CheckApplicationInstanceCertificate(false, 0).ConfigureAwait(false);

			Console.WriteLine("Creating simulator server...");
			var simulatorServer = new SimulatorServer(() => _root, nm => _nodeManager = nm);
			
			Console.WriteLine("Starting application...");
			try 
			{
				await _application.Start(simulatorServer).ConfigureAwait(false);
			}
			catch (Exception startEx)
			{
				Console.WriteLine($"Failed to start application: {startEx.Message}");
				Console.WriteLine($"Inner: {startEx.InnerException?.Message}");
				throw;
			}
			_server = simulatorServer;
			BoundEndpointUrl = endpointUrl;
			Console.WriteLine($"OPC UA Server started successfully on {endpointUrl}");

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
			catch (Exception ex)
			{
				// Log the specific error for debugging
				System.Diagnostics.Debug.WriteLine($"OPC UA Server startup failed: {ex.Message}");
				System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
				
				// Also output to console for easier debugging
				Console.WriteLine($"ERROR: OPC UA Server startup failed: {ex.Message}");
				Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");
				
				// Clean up on failure
				_server = null;
				_nodeManager = null;
				throw new InvalidOperationException($"Failed to start OPC UA server: {ex.Message}", ex);
			}
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
			try
			{
				Console.WriteLine("SimulatorNodeManager: Creating address space...");
				
				// IMPORTANT: Call base class first to initialize predefined nodes
				Console.WriteLine("SimulatorNodeManager: Calling base CreateAddressSpace...");
				base.CreateAddressSpace(externalReferences);
				
				lock (Lock)
				{
					Console.WriteLine("SimulatorNodeManager: Creating MTP folder directly...");
					
					// Create MTP folder with a unique NodeId in our namespace
					var mtpNodeId = new NodeId("MTP", NamespaceIndexes[0]);
					var mtpFolder = CreateFolder(null, mtpNodeId, "MTP", "MTP");
					
					// Add it to predefined nodes
					AddPredefinedNode(SystemContext, mtpFolder);
					
					// Add reference to Objects folder in external references
					if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var objectRefs))
					{
						objectRefs = new List<IReference>();
						externalReferences[ObjectIds.ObjectsFolder] = objectRefs;
					}
					
					// Add forward reference from Objects to MTP
					objectRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, mtpFolder.NodeId));
					
					// Add inverse reference from MTP to Objects
					if (!externalReferences.TryGetValue(mtpFolder.NodeId, out var mtpRefs))
					{
						mtpRefs = new List<IReference>();
						externalReferences[mtpFolder.NodeId] = mtpRefs;
					}
					mtpRefs.Add(new NodeStateReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder));
					
					Console.WriteLine("SimulatorNodeManager: Checking for MTP root...");
					// Add the MTP nodes if available
					var root = _rootProvider();
					if (root != null)
					{
						Console.WriteLine("SimulatorNodeManager: MTP root found, adding nodes...");
						AddNodesFromMtp(root, mtpFolder);
					}
					else
					{
						Console.WriteLine("SimulatorNodeManager: No MTP root, adding placeholder...");
						// Add a placeholder node to show the folder is working
						var placeholderVar = new BaseDataVariableState(mtpFolder)
						{
							BrowseName = new QualifiedName("Status", NamespaceIndexes[0]),
							DisplayName = "Status",
							NodeId = new NodeId("MTP_Status", NamespaceIndexes[0]),
							DataType = DataTypeIds.String,
							ValueRank = ValueRanks.Scalar,
							AccessLevel = AccessLevels.CurrentRead,
							UserAccessLevel = AccessLevels.CurrentRead,
							Value = "No MTP loaded"
						};
						mtpFolder.AddChild(placeholderVar);
						AddPredefinedNode(SystemContext, placeholderVar);
					}
					Console.WriteLine("SimulatorNodeManager: Address space creation completed successfully");
			}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"SimulatorNodeManager: ERROR in CreateAddressSpace: {ex.Message}");
				Console.WriteLine($"SimulatorNodeManager: Stack trace: {ex.StackTrace}");
				throw;
			}
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

		private FolderState CreateFolder(NodeState parent, NodeId nodeId, string browseName, string displayName)
		{
			var folder = new FolderState(parent)
			{
				SymbolicName = browseName,
				ReferenceTypeId = ReferenceTypeIds.Organizes,
				TypeDefinitionId = ObjectTypeIds.FolderType,
				NodeId = nodeId,
				BrowseName = new QualifiedName(browseName, nodeId.NamespaceIndex),
				DisplayName = displayName,
				WriteMask = AttributeWriteMask.None,
				UserWriteMask = AttributeWriteMask.None
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
			// Skip creating nested MTP folders - flatten the structure
			if (node.NodeClass == "Folder" && node.BrowseName == "MTP")
			{
				Console.WriteLine($"SimulatorNodeManager: Skipping nested MTP folder, processing {node.Children.Count} children directly");
				// Don't create another MTP folder, just process the children
				foreach (var child in node.Children)
				{
					AddNodesFromMtp(child, parent);
				}
				return;
			}

			if (node.NodeClass == "Variable")
			{
				// Create a consistent NodeId in our namespace
				var nodeId = !string.IsNullOrWhiteSpace(node.NodeId) 
					? new NodeId(node.BrowseName, NamespaceIndexes[0])  // Use BrowseName for consistency
					: new NodeId(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), NamespaceIndexes[0]);

				Console.WriteLine($"SimulatorNodeManager: Adding variable '{node.DisplayName}' with NodeId '{nodeId}'");
				
				var varState = new BaseDataVariableState(parent)
				{
					BrowseName = new QualifiedName(node.BrowseName, NamespaceIndexes[0]),
					DisplayName = node.DisplayName,
					Description = null,
					NodeId = nodeId,
					DataType = GetDataTypeId(node.DataType),
					ValueRank = ValueRanks.Scalar,
					AccessLevel = AccessLevels.CurrentReadOrWrite,
					UserAccessLevel = AccessLevels.CurrentReadOrWrite,
					Historizing = false,
					Value = GetDefaultValue(node.DataType),
					TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
					ReferenceTypeId = ReferenceTypeIds.HasComponent
				};

				parent.AddChild(varState);
				_variables[varState.NodeId.ToString()] = varState;
				
				// Add to predefined nodes so it's properly registered with the server
				AddPredefinedNode(SystemContext, varState);
				Console.WriteLine($"SimulatorNodeManager: Variable '{node.DisplayName}' added and registered with NodeId '{varState.NodeId}'");

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

			// For other folders/objects, create a container only if it's not another MTP folder
			if (node.NodeClass == "Folder" && !string.IsNullOrEmpty(node.DisplayName))
			{
				Console.WriteLine($"SimulatorNodeManager: Creating folder '{node.DisplayName}' with {node.Children.Count} children");
				var folder = CreateFolder(parent, node.BrowseName, node.DisplayName);
				foreach (var child in node.Children)
				{
					AddNodesFromMtp(child, folder);
				}
			}
			else
			{
				// For other node types, just process children directly
				foreach (var child in node.Children)
				{
					AddNodesFromMtp(child, parent);
				}
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
				Console.WriteLine($"SimulatorNodeManager: Updating value for NodeId '{nodeId}' to '{value}'");
				varState.Value = value;
				varState.Timestamp = DateTime.UtcNow;
				varState.ClearChangeMasks(SystemContext, false);
			}
			else
			{
				Console.WriteLine($"SimulatorNodeManager: WARNING - NodeId '{nodeId}' not found in variables dictionary");
				Console.WriteLine($"SimulatorNodeManager: Available NodeIds: {string.Join(", ", _variables.Keys)}");
			}
		}

		public void RefreshAddressSpace()
		{
			lock (Lock)
			{
				// Clear existing variables
				_variables.Clear();
				
				// Find the MTP folder
				var mtpFolder = FindPredefinedNode(new NodeId("MTP", NamespaceIndexes[0]), typeof(FolderState)) as FolderState;
				if (mtpFolder == null)
					return;

				// Clear existing children by removing them one by one
				var childrenToRemove = new List<BaseInstanceState>();
				mtpFolder.GetChildren(SystemContext, childrenToRemove);
				foreach (var child in childrenToRemove)
				{
					mtpFolder.RemoveChild(child);
				}

				// Add the MTP nodes if available
				var root = _rootProvider();
				if (root != null)
				{
					AddNodesFromMtp(root, mtpFolder);
				}
				else
				{
					// Add a placeholder node to show the folder is working
					var placeholderVar = new BaseDataVariableState(mtpFolder)
					{
						BrowseName = new QualifiedName("Status", NamespaceIndexes[0]),
						DisplayName = "Status",
						NodeId = new NodeId("MTP_Status", NamespaceIndexes[0]),
						DataType = DataTypeIds.String,
						ValueRank = ValueRanks.Scalar,
						AccessLevel = AccessLevels.CurrentRead,
						UserAccessLevel = AccessLevels.CurrentRead,
						Value = "No MTP loaded"
					};
					mtpFolder.AddChild(placeholderVar);
					AddPredefinedNode(SystemContext, placeholderVar);
				}
			}
		}
	}
}

