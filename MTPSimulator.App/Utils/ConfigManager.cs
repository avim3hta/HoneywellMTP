using System.IO;
using System.Text.Json;
using MTPSimulator.App.Models;

namespace MTPSimulator.App.Utils
{
    public static class ConfigManager
    {
        private const string FileName = "simconfig.json";

        public static SimulationConfig LoadOrDefault()
        {
            if (File.Exists(FileName))
            {
                try
                {
                    var json = File.ReadAllText(FileName);
                    var cfg = JsonSerializer.Deserialize<SimulationConfig>(json);
                    if (cfg != null) return cfg;
                }
                catch { }
            }
            return new SimulationConfig();
        }

        public static void Save(SimulationConfig config)
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(FileName, json);
        }
    }
}

