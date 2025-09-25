using System;

namespace MTPSimulator.App.Utils
{
    public static class DataGenerators
    {
        public static double NextSine(double tSeconds, double amplitude = 1.0, double periodSeconds = 60.0)
        {
            var omega = 2.0 * Math.PI / Math.Max(1e-6, periodSeconds);
            return amplitude * Math.Sin(omega * tSeconds);
        }

        public static double NextRamp(double current, double step, double min, double max)
        {
            var next = current + step;
            if (next > max) next = min;
            return next;
        }

        public static double AddNoise(double value, double noiseAmplitude, Random rng)
        {
            return value + (rng.NextDouble() * 2.0 - 1.0) * noiseAmplitude;
        }
    }
}

