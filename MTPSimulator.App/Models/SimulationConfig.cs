namespace MTPSimulator.App.Models
{
    public sealed class SimulationConfig
    {
        public double UpdateIntervalMs { get; set; } = 1000;
        public double NoiseAmplitude { get; set; } = 0.1;
        public double RampStep { get; set; } = 0.5;
    }
}

