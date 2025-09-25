using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MTPSimulator.App.Models;
using MTPSimulator.App.Utils;

namespace MTPSimulator.App.Core
{
    public sealed class SimulationEngine
    {
        private readonly SimulationConfig _config;
        private readonly ConcurrentDictionary<string, double> _numericValues = new();
        private readonly Random _rng = new();
        private CancellationTokenSource? _cts;
        private Task? _task;

        public event Action<string, object>? ValueUpdated;

        public SimulationEngine(SimulationConfig config)
        {
            _config = config;
        }

        public void Initialize(MTPNode root)
        {
            foreach (var n in Enumerate(root))
            {
                if (n.NodeClass == "Variable" && n.DataType == "Double")
                {
                    _numericValues[n.NodeId ?? n.DisplayName] = 0.0;
                }
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _task = Task.Run(async () =>
            {
                var start = DateTime.UtcNow;
                while (!token.IsCancellationRequested)
                {
                    var t = (DateTime.UtcNow - start).TotalSeconds;
                    foreach (var key in _numericValues.Keys)
                    {
                        var current = _numericValues[key];
                        var val = DataGenerators.NextSine(t, 50.0, 30.0);
                        val = DataGenerators.AddNoise(val, _config.NoiseAmplitude, _rng);
                        _numericValues[key] = val;
                        ValueUpdated?.Invoke(key, val);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(_config.UpdateIntervalMs), token).ConfigureAwait(false);
                }
            }, token);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); _task?.Wait(1000); }
            catch { }
            finally { _cts?.Dispose(); _cts = null; _task = null; }
        }

        private static IEnumerable<MTPNode> Enumerate(MTPNode node)
        {
            yield return node;
            foreach (var c in node.Children)
            {
                foreach (var d in Enumerate(c))
                    yield return d;
            }
        }
    }
}

