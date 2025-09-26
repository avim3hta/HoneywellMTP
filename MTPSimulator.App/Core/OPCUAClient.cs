using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;

namespace MTPSimulator.App.Core
{
    public class OPCUAClient
    {
        private Session? _session;
        private readonly ApplicationConfiguration _config;

        public OPCUAClient()
        {
            _config = CreateClientConfiguration();
        }

        public async Task<bool> ConnectAsync(string endpointUrl)
        {
            try
            {
                // Normalize endpoint URL
                var normalizedUrl = NormalizeEndpoint(endpointUrl);
                Console.WriteLine($"Connecting to: {normalizedUrl}");
                
                var endpoint = CoreClientUtils.SelectEndpoint(normalizedUrl, false);
                var endpointConfiguration = EndpointConfiguration.Create(_config);
                var configuredEndpoint = new ConfiguredEndpoint(null, endpoint, endpointConfiguration);

                _session = await Session.Create(
                    _config,
                    configuredEndpoint,
                    false,
                    "MTP Simulator Client",
                    60000,
                    null,
                    null);

                Console.WriteLine($"Connected: {_session.Connected}");
                return _session.Connected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to OPC UA server: {ex.Message}");
                return false;
            }
        }

        private static string NormalizeEndpoint(string input)
        {
            var trimmed = input.Trim();
            if (trimmed.StartsWith("opc.tcp://", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            
            // Handle formats like "localhost:4840" or "127.0.0.1:4840"
            if (trimmed.Contains(":"))
                return $"opc.tcp://{trimmed}";
            
            // Handle just "localhost" or "127.0.0.1"
            return $"opc.tcp://{trimmed}:4840";
        }

        public void Disconnect()
        {
            _session?.Close();
            _session?.Dispose();
            _session = null;
        }

        public async Task<List<NodeData>> ReadNodeValuesAsync(List<string> nodeIds)
        {
            var results = new List<NodeData>();
            
            if (_session == null || !_session.Connected)
            {
                Console.WriteLine("Session not connected");
                return results;
            }

            try
            {
                var nodesToRead = new ReadValueIdCollection();
                
                foreach (var nodeId in nodeIds)
                {
                    nodesToRead.Add(new ReadValueId
                    {
                        NodeId = new NodeId(nodeId),
                        AttributeId = Attributes.Value
                    });
                }

                var response = await _session.ReadAsync(null, 0, TimestampsToReturn.Both, nodesToRead, CancellationToken.None);
                
                for (int i = 0; i < response.Results.Count; i++)
                {
                    var result = response.Results[i];
                    if (StatusCode.IsGood(result.StatusCode))
                    {
                        results.Add(new NodeData
                        {
                            NodeId = nodeIds[i],
                            Value = result.Value,
                            DataType = result.Value?.GetType().Name ?? "Unknown",
                            Timestamp = result.SourceTimestamp
                        });
                    }
                    else
                    {
                        Console.WriteLine($"Failed to read {nodeIds[i]}: {result.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading node values: {ex.Message}");
            }

            return results;
        }

        public async Task<List<NodeData>> BrowseNodesAsync(string? parentNodeId = null)
        {
            var results = new List<NodeData>();
            
            if (_session == null || !_session.Connected)
            {
                Console.WriteLine("Session not connected");
                return results;
            }

            try
            {
                var nodeId = string.IsNullOrEmpty(parentNodeId) ? ObjectIds.ObjectsFolder : new NodeId(parentNodeId);
                
                var browseDescription = new BrowseDescriptionCollection
                {
                    new BrowseDescription
                    {
                        NodeId = nodeId,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                        IncludeSubtypes = true,
                        NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                        ResultMask = (uint)BrowseResultMask.All
                    }
                };

                var response = await _session.BrowseAsync(null, null, 0, browseDescription, CancellationToken.None);
                
                foreach (var result in response.Results)
                {
                    if (StatusCode.IsGood(result.StatusCode))
                    {
                        foreach (var reference in result.References)
                        {
                            results.Add(new NodeData
                            {
                                NodeId = reference.NodeId.ToString(),
                                DisplayName = reference.DisplayName.Text,
                                NodeClass = reference.NodeClass.ToString(),
                                DataType = reference.TypeDefinition?.ToString() ?? "Unknown"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error browsing nodes: {ex.Message}");
            }

            return results;
        }

        public async Task<bool> WriteNodeValueAsync(string nodeId, object value)
        {
            if (_session == null || !_session.Connected)
            {
                Console.WriteLine("Session not connected");
                return false;
            }

            try
            {
                var nodesToWrite = new WriteValueCollection
                {
                    new WriteValue
                    {
                        NodeId = new NodeId(nodeId),
                        AttributeId = Attributes.Value,
                        Value = new DataValue(new Variant(value))
                    }
                };

                var response = await _session.WriteAsync(null, nodesToWrite, CancellationToken.None);
                
                return StatusCode.IsGood(response.Results[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing node value: {ex.Message}");
                return false;
            }
        }

        private static ApplicationConfiguration CreateClientConfiguration()
        {
            var config = new ApplicationConfiguration
            {
                ApplicationName = "MTP Simulator Client",
                ApplicationUri = $"urn:{Environment.MachineName}:MTPSimulatorClient",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = CertificateStoreType.X509Store,
                        StorePath = "CurrentUser\\My",
                        SubjectName = "CN=MTP Simulator Client"
                    },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = false
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
            };

            return config;
        }
    }

    public class NodeData
    {
        public string NodeId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NodeClass { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public object? Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
