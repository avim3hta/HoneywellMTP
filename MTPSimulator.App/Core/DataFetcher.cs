using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MTPSimulator.App.Models;

namespace MTPSimulator.App.Core
{
    public class DataFetcher
    {
        private readonly OPCUAClient _client;
        private readonly Dictionary<string, string> _nodeMappings;
        private CancellationTokenSource? _cts;
        private Task? _fetchTask;

        public event Action<string, object>? DataReceived;

        public DataFetcher()
        {
            _client = new OPCUAClient();
            _nodeMappings = new Dictionary<string, string>();
        }

        public async Task<bool> ConnectToServerAsync(string endpointUrl)
        {
            return await _client.ConnectAsync(endpointUrl);
        }

        public void Disconnect()
        {
            StopFetching();
            _client.Disconnect();
        }

        public void AddNodeMapping(string localNodeId, string remoteNodeId)
        {
            _nodeMappings[localNodeId] = remoteNodeId;
        }

        public void RemoveNodeMapping(string localNodeId)
        {
            _nodeMappings.Remove(localNodeId);
        }

        public void StartFetching(int intervalMs = 1000)
        {
            StopFetching();
            
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            
            _fetchTask = Task.Run(async () =>
            {
                Console.WriteLine("Starting data fetching...");
                
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await FetchDataAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching data: {ex.Message}");
                    }

                    await Task.Delay(intervalMs, token);
                }
                
                Console.WriteLine("Data fetching stopped");
            }, token);
        }

        public void StopFetching()
        {
            try
            {
                _cts?.Cancel();
                _fetchTask?.Wait(1000);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _fetchTask = null;
            }
        }

        private async Task FetchDataAsync()
        {
            if (_nodeMappings.Count == 0)
                return;

            var remoteNodeIds = new List<string>(_nodeMappings.Values);
            var nodeData = await _client.ReadNodeValuesAsync(remoteNodeIds);

            foreach (var data in nodeData)
            {
                // Find the local node ID for this remote node
                foreach (var mapping in _nodeMappings)
                {
                    if (mapping.Value == data.NodeId)
                    {
                        DataReceived?.Invoke(mapping.Key, data.Value ?? 0.0);
                        break;
                    }
                }
            }
        }

        public async Task<List<NodeData>> BrowseRemoteNodesAsync(string? parentNodeId = null)
        {
            return await _client.BrowseNodesAsync(parentNodeId);
        }

        public int GetMappingCount()
        {
            return _nodeMappings.Count;
        }

        public Dictionary<string, string> GetNodeMappings()
        {
            return new Dictionary<string, string>(_nodeMappings);
        }

        public async Task<bool> WriteToRemoteNodeAsync(string nodeId, object value)
        {
            return await _client.WriteNodeValueAsync(nodeId, value);
        }
    }
}
