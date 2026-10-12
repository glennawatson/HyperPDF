#!/usr/bin/env -S dotnet run --file
#:package Microsoft.Diagnostics.Tracing.TraceEvent
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Reads the CPU sample traces the benchmarks write when run with BenchmarkDotNet's "--profiler EP" and reports where
// each measured operation spends its time. Given a baseline, it flags any benchmark, or any method taking a fair share
// of an operation, that got slower by more than the leeway: timings on shared machines move a few percent, so the
// default leeway is 5%. scripts/compare-benchmarks.sh runs it.
// Several runs may be given, separated by commas, and are paired with the baseline runs in order; growth must show in
// every pair, so drift or a spike that disturbed one run does not count. scripts/compare-benchmarks.sh runs the
// baseline and the change in the order A, B, B, A, so each pair ran side by side.
// Usage: dotnet run --file scripts/CpuAudit.cs -- <traces> [--baseline <baseline traces>] [--leeway <percent>]
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;

return HyperPdf.Scripts.CpuAudit.Run(args);

namespace HyperPdf.Scripts
{
    /// <summary>
    /// Attributes the CPU samples taken while BenchmarkDotNet runs its measured iterations to the methods on their
    /// stacks, and turns each method's share of the samples into time per operation.
    /// </summary>
    internal static class CpuAudit
    {
        /// <summary>The exit code when nothing grew past the leeway.</summary>
        private const int Success = 0;

        /// <summary>The exit code when something grew past the leeway.</summary>
        private const int Grew = 1;

        /// <summary>The exit code for bad arguments.</summary>
        private const int Usage = 2;

        /// <summary>The namespace prefix of the code under audit.</summary>
        private const string LibraryPrefix = "HyperPdf.";

        /// <summary>The namespace prefix of the managed PDF engine and its drawing backend.</summary>
        private const string HyperPdfPrefix = "HyperPdfLibrary.";

        /// <summary>The namespace prefix of the benchmark classes.</summary>
        private const string BenchmarkPrefix = "HyperPdf.Benchmarks.";

        /// <summary>The option naming the baseline traces.</summary>
        private const string BaselineOption = "--baseline";

        /// <summary>The option setting the leeway, in percent.</summary>
        private const string LeewayOption = "--leeway";

        /// <summary>The default leeway, in percent.</summary>
        private const double DefaultLeeway = 5;

        /// <summary>The share of an operation a method must take to be reported or compared, in percent.</summary>
        private const double MinShare = 2;

        /// <summary>The least growth, as a share of the baseline operation, that counts, so tiny methods are not noise.</summary>
        private const double MinGrowthShare = 1;

        /// <summary>The methods listed per benchmark.</summary>
        private const int Listed = 12;

        /// <summary>A whole in percent.</summary>
        private const double Percent = 100;

        /// <summary>Nanoseconds in a microsecond.</summary>
        private const double NanosecondsPerMicrosecond = 1000;

        /// <summary>Entry point.</summary>
        /// <param name="args">The traces, and optionally the baseline and the leeway.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var output = Console.Out;
            if (args.Length == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
            {
                output.WriteLine("usage: cpu-audit <traces> [--baseline <baseline traces>] [--leeway <percent>]");
                return Usage;
            }

            var leeway = Option(args, LeewayOption) is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : DefaultLeeway;
            var current = ReadRuns(args[0]);
            foreach (var benchmark in current[0])
            {
                Report(output, benchmark);
            }

            if (Option(args, BaselineOption) is not { } baselinePath)
            {
                return Success;
            }

            var grew = Compare(output, ReadRuns(baselinePath), current, leeway);
            output.WriteLine(grew == 0
                ? string.Create(CultureInfo.InvariantCulture, $"Nothing grew past the {leeway:0.#}% leeway.")
                : string.Create(CultureInfo.InvariantCulture, $"{grew} timing(s) grew past the {leeway:0.#}% leeway."));
            return grew == 0 ? Success : Grew;
        }

        /// <summary>Gets an option's value.</summary>
        /// <param name="args">The arguments.</param>
        /// <param name="name">The option.</param>
        /// <returns>The value, or <see langword="null"/> when absent.</returns>
        private static string? Option(string[] args, string name)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        /// <summary>Reads the runs under one or more paths.</summary>
        /// <param name="paths">Trace files or folders, one per run, separated by commas.</param>
        /// <returns>Each run's benchmarks, in the order given.</returns>
        private static List<List<BenchmarkProfile>> ReadRuns(string paths)
        {
            var runs = new List<List<BenchmarkProfile>>();
            foreach (var path in paths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var traces = Directory.Exists(path) ? Directory.GetFiles(path, "*.nettrace", SearchOption.AllDirectories) : [path];
                var run = new List<BenchmarkProfile>(traces.Length);
                foreach (var trace in traces)
                {
                    if (Read(trace) is { } benchmark)
                    {
                        run.Add(benchmark);
                    }
                }

                run.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
                runs.Add(run);
            }

            return runs;
        }

        /// <summary>Prints one benchmark's time per operation and its busiest methods.</summary>
        /// <param name="output">The report writer.</param>
        /// <param name="benchmark">The benchmark.</param>
        private static void Report(TextWriter output, BenchmarkProfile benchmark)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{benchmark.Name}: {benchmark.MicrosecondsPerOperation:N3} us/op, {benchmark.Samples:N0} samples over {benchmark.Operations:N0} operations"));
            output.WriteLine("  with callees:");
            List(output, benchmark.Methods);
            output.WriteLine("  own code:");
            List(output, benchmark.Self);
        }

        /// <summary>Prints the busiest methods of a list.</summary>
        /// <param name="output">The report writer.</param>
        /// <param name="methods">The methods, busiest first.</param>
        private static void List(TextWriter output, List<MethodTime> methods)
        {
            var listed = 0;
            foreach (var method in methods)
            {
                if (listed == Listed || method.Share < MinShare)
                {
                    break;
                }

                listed++;
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"    {method.Share:0.0}%  {method.MicrosecondsPerOperation:N3} us/op  {method.Name}"));
            }
        }

        /// <summary>
        /// Compares each benchmark and its busiest methods with the baseline, run by run: the first change run with the
        /// first baseline run, and so on. Taken in the order baseline, change, change, baseline, each pair ran side by
        /// side, so a machine that slows down over the runs moves both halves of a pair alike. Only growth past the
        /// leeway in every pair counts; a spike that disturbed one run shows in one pair only.
        /// </summary>
        /// <param name="output">The report writer.</param>
        /// <param name="baseline">The baseline runs.</param>
        /// <param name="current">The runs being checked.</param>
        /// <param name="leeway">The leeway, in percent.</param>
        /// <returns>The number of timings that grew past the leeway in every pair.</returns>
        private static int Compare(TextWriter output, List<List<BenchmarkProfile>> baseline, List<List<BenchmarkProfile>> current, double leeway)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Compared with the baseline run by run ({leeway:0.#}% leeway, growth must show in every pair):"));
            var grew = 0;
            foreach (var benchmark in current[0])
            {
                var pairs = Pairs(baseline, current, benchmark.Name);
                if (pairs.Count == 0)
                {
                    output.WriteLine($"  {benchmark.Name}: not in the baseline");
                    continue;
                }

                var changes = new List<double>(pairs.Count);
                var line = new StringBuilder().Append(CultureInfo.InvariantCulture, $"  {benchmark.Name}:");
                foreach (var (before, now) in pairs)
                {
                    var change = Change(before.MicrosecondsPerOperation, now.MicrosecondsPerOperation);
                    changes.Add(change);
                    _ = line.Append(CultureInfo.InvariantCulture, $"  {before.MicrosecondsPerOperation:N3} -> {now.MicrosecondsPerOperation:N3} us/op ({change:+0.0;-0.0}%)");
                }

                var slower = changes.TrueForAll(c => c > leeway);
                grew += slower ? 1 : 0;
                output.WriteLine(line.Append(slower ? "  SLOWER" : string.Empty));
                grew += CompareMethods(output, pairs, changes, leeway);
            }

            return grew;
        }

        /// <summary>Pairs a benchmark's runs: the first baseline run with the first run checked, and so on.</summary>
        /// <param name="baseline">The baseline runs.</param>
        /// <param name="current">The runs being checked.</param>
        /// <param name="name">The benchmark.</param>
        /// <returns>The pairs where both runs measured the benchmark.</returns>
        private static List<(BenchmarkProfile Before, BenchmarkProfile Now)> Pairs(List<List<BenchmarkProfile>> baseline, List<List<BenchmarkProfile>> current, string name)
        {
            var pairs = new List<(BenchmarkProfile Before, BenchmarkProfile Now)>();
            for (var i = 0; i < Math.Min(baseline.Count, current.Count); i++)
            {
                if (baseline[i].Find(b => b.Name == name) is { } before && current[i].Find(b => b.Name == name) is { } now)
                {
                    pairs.Add((before, now));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Lists HyperPDF methods that grew out of proportion in every pair: more than the leeway beyond the
        /// benchmark's own change, so a machine that ran slower does not make every method look worse. Runtime methods
        /// are left out: the JIT inlines them differently from build to build, which moves their samples between frames.
        /// </summary>
        /// <param name="output">The report writer.</param>
        /// <param name="pairs">The baseline and checked runs, pair by pair.</param>
        /// <param name="changes">The benchmark's own change in each pair, in percent.</param>
        /// <param name="leeway">The leeway, in percent.</param>
        /// <returns>The number of methods that grew.</returns>
        private static int CompareMethods(TextWriter output, List<(BenchmarkProfile Before, BenchmarkProfile Now)> pairs, List<double> changes, double leeway)
        {
            var grew = 0;
            foreach (var method in pairs[0].Now.Methods)
            {
                if (method.Share < MinShare)
                {
                    break;
                }

                if (!IsLibrary(method.Name))
                {
                    continue;
                }

                var (isNew, grewInEveryPair) = MethodGrowth(method.Name, pairs, changes, leeway);
                if (isNew)
                {
                    // A method the baseline did not have is often a rename or a split; the benchmark's own time says
                    // whether the work grew.
                    output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"    new in profile ({method.Share:0.0}% of the operation)  {method.Name}"));
                    continue;
                }

                if (!grewInEveryPair)
                {
                    continue;
                }

                grew++;
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"    GREW in every pair ({method.Share:0.0}% of the operation)  {method.Name}"));
            }

            return grew;
        }

        /// <summary>Decides how a method's time changed across the pairs.</summary>
        /// <param name="name">The method.</param>
        /// <param name="pairs">The baseline and checked runs, pair by pair.</param>
        /// <param name="changes">The benchmark's own change in each pair, in percent.</param>
        /// <param name="leeway">The leeway, in percent.</param>
        /// <returns>Whether the baseline lacked it, and whether it grew out of proportion in every pair.</returns>
        private static (bool IsNew, bool Grew) MethodGrowth(string name, List<(BenchmarkProfile Before, BenchmarkProfile Now)> pairs, List<double> changes, double leeway)
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                var (before, now) = pairs[i];
                var old = before.Methods.Find(m => m.Name == name);
                var current = now.Methods.Find(m => m.Name == name);
                if (old is null || current is null)
                {
                    return (old is null, false);
                }

                var floor = before.MicrosecondsPerOperation * MinGrowthShare / Percent;
                if (current.MicrosecondsPerOperation - old.MicrosecondsPerOperation < floor
                    || Change(old.MicrosecondsPerOperation, current.MicrosecondsPerOperation) - Math.Max(changes[i], 0) <= leeway)
                {
                    return (false, false);
                }
            }

            return (false, true);
        }

        /// <summary>Gets whether a method is HyperPDF code under audit, not a benchmark or the runtime.</summary>
        /// <param name="name">The method.</param>
        /// <returns><see langword="true"/> for library code.</returns>
        private static bool IsLibrary(string name) =>
            name.StartsWith(HyperPdfPrefix, StringComparison.Ordinal)
            || (name.StartsWith(LibraryPrefix, StringComparison.Ordinal) && !name.StartsWith(BenchmarkPrefix, StringComparison.Ordinal));

        /// <summary>Gets the change from one value to another, in percent.</summary>
        /// <param name="was">The baseline value.</param>
        /// <param name="now">The value now.</param>
        /// <returns>The change; infinite when the baseline was zero and the value grew.</returns>
        private static double Change(double was, double now)
        {
            if (was > 0)
            {
                return (now - was) / was * Percent;
            }

            return now > 0 ? double.PositiveInfinity : 0;
        }

        /// <summary>Reads a trace's samples taken inside measured iterations.</summary>
        /// <param name="tracePath">The .nettrace file.</param>
        /// <returns>The benchmark's profile, or <see langword="null"/> when the trace measured no benchmark.</returns>
        private static BenchmarkProfile? Read(string tracePath)
        {
            var etlx = TraceLog.CreateFromEventPipeDataFile(tracePath);
            try
            {
                using var log = new TraceLog(etlx);
                var reader = new SampleReader();
                foreach (var data in log.Events)
                {
                    reader.Read(data);
                }

                return reader.Name is null || reader.Operations == 0 || reader.Samples == 0 ? null : Profile(reader);
            }
            finally
            {
                File.Delete(etlx);
            }
        }

        /// <summary>Turns a benchmark's samples into time per operation, for the benchmark and each method.</summary>
        /// <param name="reader">The benchmark's samples.</param>
        /// <returns>The profile.</returns>
        private static BenchmarkProfile Profile(SampleReader reader)
        {
            var perOperation = reader.Nanoseconds / NanosecondsPerMicrosecond / reader.Operations;
            return new(reader.Name!, reader.Operations, reader.Samples, perOperation, Times(reader.Inclusive, reader.Samples, perOperation), Times(reader.Self, reader.Samples, perOperation));
        }

        /// <summary>Turns sample counts into shares and time per operation, busiest first.</summary>
        /// <param name="counts">The samples per method.</param>
        /// <param name="samples">All the samples.</param>
        /// <param name="perOperation">The time per operation.</param>
        /// <returns>The methods.</returns>
        private static List<MethodTime> Times(Dictionary<string, long> counts, long samples, double perOperation)
        {
            var methods = new List<MethodTime>(counts.Count);
            foreach (var (name, count) in counts)
            {
                var share = (double)count / samples;
                methods.Add(new(name, share * Percent, share * perOperation));
            }

            methods.Sort(static (a, b) => b.Share.CompareTo(a.Share));
            return methods;
        }

        /// <summary>
        /// Follows a trace in order: the benchmark's name, the measured iterations BenchmarkDotNet marks with its engine
        /// events, and the CPU samples of the thread running the workload inside them.
        /// </summary>
        [DebuggerDisplay("SampleReader: {Name}, {Samples} samples")]
        internal sealed class SampleReader
        {
            /// <summary>The engine's event source.</summary>
            private const string EngineSource = "BenchmarkDotNet.EngineEventSource";

            /// <summary>The sample profiler's provider.</summary>
            private const string SampleProvider = "Microsoft-DotNETCore-SampleProfiler";

            /// <summary>The frame BenchmarkDotNet runs the measured workload in.</summary>
            private const string WorkloadMarker = "WorkloadAction";

            /// <summary>The frames of the current sample inside the workload, innermost first.</summary>
            private readonly List<string> _frames = [];

            /// <summary>The frames already counted for the current sample, so recursion counts once.</summary>
            private readonly HashSet<string> _seen = [];

            /// <summary>When the current measured iteration started, or -1 outside one.</summary>
            private double _started = -1;

            /// <summary>Gets the benchmark's full name, or <see langword="null"/> before it starts.</summary>
            internal string? Name { get; private set; }

            /// <summary>Gets the operations the measured iterations ran.</summary>
            internal long Operations { get; private set; }

            /// <summary>Gets the time the measured iterations took, in nanoseconds.</summary>
            internal double Nanoseconds { get; private set; }

            /// <summary>Gets the workload samples taken in measured iterations.</summary>
            internal long Samples { get; private set; }

            /// <summary>Gets the samples each method was on the stack for.</summary>
            internal Dictionary<string, long> Inclusive { get; } = [];

            /// <summary>Gets the samples each method was the sampled frame for.</summary>
            internal Dictionary<string, long> Self { get; } = [];

            /// <summary>Reads one event.</summary>
            /// <param name="data">The event.</param>
            internal void Read(TraceEvent data)
            {
                if (data.ProviderName == EngineSource)
                {
                    ReadEngine(data);
                    return;
                }

                if (_started >= 0 && data.ProviderName == SampleProvider)
                {
                    Record(data.CallStack());
                }
            }

            /// <summary>Follows the engine's iteration events.</summary>
            /// <param name="data">The engine event.</param>
            private void ReadEngine(TraceEvent data)
            {
                switch (data.EventName)
                {
                    case "Benchmark/Start":
                    {
                        Name = data.PayloadByName("benchmarkName") as string;
                        break;
                    }

                    case "WorkloadActual/Start":
                    {
                        _started = data.TimeStampRelativeMSec;
                        Operations += Convert.ToInt64(data.PayloadByName("totalOperations"), CultureInfo.InvariantCulture);
                        break;
                    }

                    case "WorkloadActual/Stop" when _started >= 0:
                    {
                        Nanoseconds += (data.TimeStampRelativeMSec - _started) * NanosecondsPerMicrosecond * NanosecondsPerMicrosecond;
                        _started = -1;
                        break;
                    }
                }
            }

            /// <summary>
            /// Counts a sample of the workload thread: once for each method between the sampled frame and the workload
            /// loop, so the harness above it and recursion are left out, and once as self time for the sampled frame.
            /// </summary>
            /// <param name="stack">The call stack, innermost first.</param>
            private void Record(TraceCallStack? stack)
            {
                _frames.Clear();
                var inWorkload = false;
                for (var frame = stack; frame is not null; frame = frame.Caller)
                {
                    var name = frame.CodeAddress.FullMethodName is { Length: > 0 } method ? method : $"{frame.CodeAddress.ModuleName}!?";
                    if (name.Contains(WorkloadMarker, StringComparison.Ordinal))
                    {
                        inWorkload = true;
                        break;
                    }

                    _frames.Add(name);
                }

                if (!inWorkload)
                {
                    return;
                }

                Samples++;
                if (_frames.Count > 0)
                {
                    Self[_frames[0]] = Self.GetValueOrDefault(_frames[0]) + 1;
                }

                _seen.Clear();
                foreach (var name in _frames)
                {
                    if (_seen.Add(name))
                    {
                        Inclusive[name] = Inclusive.GetValueOrDefault(name) + 1;
                    }
                }
            }
        }

        /// <summary>What one benchmark spends per measured operation.</summary>
        /// <param name="Name">The benchmark's full name.</param>
        /// <param name="Operations">The operations the measured iterations ran.</param>
        /// <param name="Samples">The CPU samples taken in them.</param>
        /// <param name="MicrosecondsPerOperation">The measured time per operation.</param>
        /// <param name="Methods">Each method's share of the samples with its callees, busiest first.</param>
        /// <param name="Self">Each method's share of the samples in its own code, busiest first.</param>
        [DebuggerDisplay("{Name}: {MicrosecondsPerOperation} us/op")]
        internal sealed record BenchmarkProfile(string Name, long Operations, long Samples, double MicrosecondsPerOperation, List<MethodTime> Methods, List<MethodTime> Self);

        /// <summary>The time a method and its callees take per operation.</summary>
        /// <param name="Name">The method.</param>
        /// <param name="Share">Its share of the samples, in percent.</param>
        /// <param name="MicrosecondsPerOperation">Its time per operation.</param>
        [DebuggerDisplay("{Name}: {Share}%")]
        internal sealed record MethodTime(string Name, double Share, double MicrosecondsPerOperation);
    }
}
