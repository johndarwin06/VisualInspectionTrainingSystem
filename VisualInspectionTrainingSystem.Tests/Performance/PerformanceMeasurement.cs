#region Namespaces

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

#endregion

namespace VisualInspectionTrainingSystem.Tests.Performance
{
    /// <summary>
    /// Represents warm-operation timing statistics for one deterministic workload.
    /// Timing values are evidence only; functional assertions remain the hard gate.
    /// </summary>
    internal sealed class PerformanceSampleSet
    {
        #region Constructor

        /// <summary>
        /// Initializes immutable statistics from ordered millisecond samples.
        /// </summary>
        /// <param name="name">Non-sensitive workload name.</param>
        /// <param name="samples">Measured elapsed milliseconds.</param>
        internal PerformanceSampleSet(
            string name,
            IEnumerable<double> samples)
        {
            Name = name ?? string.Empty;
            Samples = (samples ?? Enumerable.Empty<double>())
                .OrderBy(value => value)
                .ToArray();

            if (Samples.Length == 0)
                throw new ArgumentException("At least one timing sample is required.", nameof(samples));
        }

        #endregion

        #region Properties

        /// <summary>Gets the workload name.</summary>
        internal string Name { get; private set; }

        /// <summary>Gets the ordered samples.</summary>
        internal double[] Samples { get; private set; }

        /// <summary>Gets the measured sample count.</summary>
        internal int Count { get { return Samples.Length; } }

        /// <summary>Gets the minimum elapsed milliseconds.</summary>
        internal double MinimumMilliseconds { get { return Samples[0]; } }

        /// <summary>Gets the median elapsed milliseconds.</summary>
        internal double MedianMilliseconds { get { return Percentile(0.50d); } }

        /// <summary>Gets the nearest-rank p95 elapsed milliseconds.</summary>
        internal double P95Milliseconds { get { return Percentile(0.95d); } }

        /// <summary>Gets the maximum elapsed milliseconds.</summary>
        internal double MaximumMilliseconds { get { return Samples[Samples.Length - 1]; } }

        #endregion

        #region Reporting

        /// <summary>
        /// Writes a machine-readable, credential-free summary to NUnit progress output.
        /// </summary>
        internal void WriteToTestOutput()
        {
            TestContext.Progress.WriteLine(
                "PERF|{0}|Samples={1}|MinMs={2}|MedianMs={3}|P95Ms={4}|MaxMs={5}",
                Name,
                Count,
                Format(MinimumMilliseconds),
                Format(MedianMilliseconds),
                Format(P95Milliseconds),
                Format(MaximumMilliseconds));
        }

        private double Percentile(double percentile)
        {
            int rank = Math.Max(
                0,
                Math.Min(
                    Samples.Length - 1,
                    (int)Math.Ceiling(percentile * Samples.Length) - 1));

            return Samples[rank];
        }

        private static string Format(double milliseconds)
        {
            return milliseconds.ToString("0.000", CultureInfo.InvariantCulture);
        }

        #endregion
    }

    /// <summary>
    /// Executes warm-ups and repeated timing iterations without introducing a benchmark dependency.
    /// </summary>
    internal static class PerformanceMeasurement
    {
        #region Measurement

        /// <summary>
        /// Measures a synchronous workload after warm-up iterations.
        /// </summary>
        internal static PerformanceSampleSet Measure(
            string name,
            int warmupCount,
            int sampleCount,
            Action operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            ValidateCounts(warmupCount, sampleCount);

            for (int index = 0; index < warmupCount; index++)
                operation();

            List<double> samples = new List<double>(sampleCount);

            for (int index = 0; index < sampleCount; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                operation();
                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            PerformanceSampleSet result = new PerformanceSampleSet(name, samples);
            result.WriteToTestOutput();
            return result;
        }

        /// <summary>
        /// Measures an asynchronous workload after warm-up iterations.
        /// </summary>
        internal static async Task<PerformanceSampleSet> MeasureAsync(
            string name,
            int warmupCount,
            int sampleCount,
            Func<Task> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            ValidateCounts(warmupCount, sampleCount);

            for (int index = 0; index < warmupCount; index++)
                await operation();

            List<double> samples = new List<double>(sampleCount);

            for (int index = 0; index < sampleCount; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                await operation();
                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            PerformanceSampleSet result = new PerformanceSampleSet(name, samples);
            result.WriteToTestOutput();
            return result;
        }

        private static void ValidateCounts(int warmupCount, int sampleCount)
        {
            if (warmupCount < 0)
                throw new ArgumentOutOfRangeException(nameof(warmupCount));

            if (sampleCount < 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount),
                    "At least three measured iterations are required.");
            }
        }

        #endregion
    }
}
