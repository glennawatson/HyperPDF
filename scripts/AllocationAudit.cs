#!/usr/bin/env -S dotnet run --file
#:package Microsoft.Diagnostics.Tracing.TraceEvent
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Reads the allocation traces the benchmarks write (alloc-{pid}.nettrace, traced from start-up so every allocation is
// sampled with its stack) and reports what PdfViewerLite allocates per operation of each measured benchmark. Fails on
// any allocation benchmarks/allocations-explained.json does not explain, and, given a baseline, on any growth at all:
// allocations are deterministic, so memory gets no leeway. scripts/audit-allocations.sh and
// scripts/compare-benchmarks.sh run it.
// Usage: dotnet run --file scripts/AllocationAudit.cs -- <traces> [explained.json] [--baseline <baseline traces>]
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

return PdfViewerLite.Scripts.AllocationAudit.Run(args);

namespace PdfViewerLite.Scripts
{
    /// <summary>
    /// Attributes the allocations in EventPipe traces to PdfViewerLite methods, per operation of the measured
    /// workload. Only allocations made while BenchmarkDotNet runs its actual workload iterations count; set-up, warm-up
    /// and the harness are left out, and the operations those iterations ran turn the totals into bytes per operation.
    /// </summary>
    internal static partial class AllocationAudit
    {
        /// <summary>The exit code when every allocation is explained and nothing grew.</summary>
        private const int Success = 0;

        /// <summary>The exit code when an allocation is unexplained or grew.</summary>
        private const int Failed = 1;

        /// <summary>The exit code for bad arguments.</summary>
        private const int Usage = 2;

        /// <summary>The namespace prefix of the app code under audit.</summary>
        private const string LibraryPrefix = "PdfViewerLite.";

        /// <summary>The namespace prefix of the managed PDF engine under audit.</summary>
        private const string EnginePrefix = "HyperPdfLibrary.";

        /// <summary>How far from a whole count of objects per operation a site may measure and still be that count.</summary>
        private const double WholeSlack = 0.1;

        /// <summary>The option naming the baseline traces.</summary>
        private const string BaselineOption = "--baseline";

        /// <summary>Entry point.</summary>
        /// <param name="args">The traces, the optional explained allocations file and the optional baseline.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var output = Console.Out;
            var baselineAt = Array.IndexOf(args, BaselineOption);
            var positional = baselineAt >= 0 ? args[..baselineAt] : args;
            if (positional.Length == 0 || (baselineAt >= 0 && baselineAt + 1 >= args.Length))
            {
                output.WriteLine("usage: allocation-audit <traces> [explained.json] [--baseline <baseline traces>]");
                return Usage;
            }

            var explained = positional.Length > 1 ? LoadExplained(positional[1]) : [];
            var current = ReadAll(positional[0], explained);
            var failures = 0;
            foreach (var benchmark in current)
            {
                failures += Report(output, benchmark);
            }

            if (baselineAt >= 0)
            {
                failures += Compare(output, ReadAll(args[baselineAt + 1], explained), current);
            }

            output.WriteLine(failures == 0 ? "All library allocations are explained and none grew." : $"{failures} allocation problem(s).");
            return failures == 0 ? Success : Failed;
        }

        /// <summary>Reads every trace under a path.</summary>
        /// <param name="path">A trace file or a folder of them.</param>
        /// <param name="explained">The expected allocations.</param>
        /// <returns>The benchmarks, by name.</returns>
        private static List<BenchmarkAllocations> ReadAll(string path, List<ExplainedAllocation> explained)
        {
            var traces = Directory.Exists(path) ? Directory.GetFiles(path, "*.nettrace", SearchOption.AllDirectories) : [path];
            var result = new List<BenchmarkAllocations>(traces.Length);
            foreach (var trace in traces)
            {
                if (Audit(trace, explained) is { } benchmark)
                {
                    result.Add(benchmark);
                }
            }

            result.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        /// <summary>Loads the explained allocations.</summary>
        /// <param name="path">The JSON file.</param>
        /// <returns>The entries.</returns>
        private static List<ExplainedAllocation> LoadExplained(string path)
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, AuditJsonContext.Default.ListExplainedAllocation) ?? [];
        }

        /// <summary>Prints one benchmark's allocation sites.</summary>
        /// <param name="output">The report writer.</param>
        /// <param name="benchmark">The benchmark.</param>
        /// <returns>The number of unexplained sites.</returns>
        private static int Report(TextWriter output, BenchmarkAllocations benchmark)
        {
            var summary = string.Create(
                CultureInfo.InvariantCulture,
                $"{benchmark.Name}: {benchmark.BytesPerOperation:N1} B/op in {benchmark.Sites.Count} library site(s), {benchmark.Operations:N0} measured operations");
            output.WriteLine(summary);
            foreach (var type in benchmark.Types)
            {
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  type {type.BytesPerOperation:N1} B/op (+/- {type.ResolutionPerOperation:N1}), {type.ObjectsPerOperation:N2} objects/op"
                    + $"{(type.IsWhole ? string.Empty : " (amortised)")}  {type.Type}"));
            }

            var unexplained = 0;
            foreach (var site in benchmark.Sites)
            {
                var reason = site.Explanation?.Reason ?? "UNEXPLAINED";
                unexplained += site.Explanation is null ? 1 : 0;
                var line = string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {site.BytesPerOperation:N1} B/op, {site.ObjectsPerOperation:N2} objects/op{(site.IsWhole ? string.Empty : " (amortised)")},  {site.Type}  in {site.Frame}  ({reason})");
                output.WriteLine(line);
            }

            return unexplained;
        }

        /// <summary>
        /// Compares each benchmark's allocations per operation with the baseline, type by type. The runtime reports
        /// allocations in batches per type, each with the stack of the last one, so totals per type are exact while the
        /// split between sites is not. A type allocated more often per operation fails, as does a type the baseline did
        /// not allocate; bytes may differ only within the batches the measurement started or ended inside.
        /// </summary>
        /// <param name="output">The report writer.</param>
        /// <param name="baseline">The baseline benchmarks.</param>
        /// <param name="current">The benchmarks being checked.</param>
        /// <returns>The number of benchmarks and types that grew.</returns>
        private static int Compare(TextWriter output, List<BenchmarkAllocations> baseline, List<BenchmarkAllocations> current)
        {
            output.WriteLine("Compared with the baseline, type by type (no leeway beyond the trace's own resolution):");
            var grew = 0;
            foreach (var benchmark in current)
            {
                var before = baseline.Find(b => b.Name == benchmark.Name);
                if (before is null)
                {
                    output.WriteLine($"  {benchmark.Name}: not in the baseline");
                    continue;
                }

                var lines = new List<string>();
                var types = CompareTypes(lines, before, benchmark);
                grew += types;
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {benchmark.Name}: {before.BytesPerOperation:N1} -> {benchmark.BytesPerOperation:N1} B/op  {(types == 0 ? "no growth" : "GREW")}"));
                foreach (var line in lines)
                {
                    output.WriteLine(line);
                }
            }

            return grew;
        }

        /// <summary>Lists the types that grew or are new.</summary>
        /// <param name="output">Receives the report lines.</param>
        /// <param name="before">The baseline benchmark.</param>
        /// <param name="now">The benchmark being checked.</param>
        /// <returns>The number of types that grew or are new.</returns>
        private static int CompareTypes(List<string> output, BenchmarkAllocations before, BenchmarkAllocations now)
        {
            var grew = 0;
            foreach (var type in now.Types)
            {
                var old = before.Types.Find(t => t.Type == type.Type);
                if (Growth(old, type) is not { } reason)
                {
                    continue;
                }

                grew++;
                output.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"    {reason}: {old?.ObjectsPerOperation ?? 0:N2} -> {type.ObjectsPerOperation:N2} objects, {old?.BytesPerOperation ?? 0:N1} -> {type.BytesPerOperation:N1} B/op  {type.Type}"));
            }

            return grew;
        }

        /// <summary>Decides whether a type's allocations grew.</summary>
        /// <param name="old">The baseline totals, or <see langword="null"/> when the baseline did not allocate the type.</param>
        /// <param name="now">The totals now.</param>
        /// <returns>Why it grew, or <see langword="null"/> when it did not.</returns>
        private static string? Growth(TypeAllocations? old, TypeAllocations now)
        {
            if (old is null)
            {
                // A type allocated less than once per operation and within one batch may be a lazy cache filled once.
                return now.IsWhole || now.BytesPerOperation > now.ResolutionPerOperation ? "NEW" : null;
            }

            if (!old.IsWhole || !now.IsWhole)
            {
                // Allocated now and then, such as a growing buffer or a cache: its share depends on how many operations ran.
                return null;
            }

            if (now.ObjectsPerOperation > old.ObjectsPerOperation)
            {
                return "MORE OBJECTS";
            }

            return now.BytesPerOperation - old.BytesPerOperation > old.ResolutionPerOperation + now.ResolutionPerOperation ? "MORE BYTES" : null;
        }

        /// <summary>Reads a trace and groups the library allocations of its measured iterations by type and method.</summary>
        /// <param name="tracePath">The .nettrace file.</param>
        /// <param name="explained">The expected allocations.</param>
        /// <returns>The benchmark's allocations, or <see langword="null"/> when the trace measured no benchmark.</returns>
        private static BenchmarkAllocations? Audit(string tracePath, List<ExplainedAllocation> explained)
        {
            var etlx = TraceLog.CreateFromEventPipeDataFile(tracePath);
            try
            {
                using var log = new TraceLog(etlx);
                var reader = new TraceReader();
                foreach (var data in log.Events)
                {
                    reader.Read(data);
                }

                if (reader.Name is null || reader.Operations == 0)
                {
                    return null;
                }

                var sites = new List<AllocationSite>(reader.Sites.Count);
                foreach (var ((type, frame), (count, bytes)) in reader.Sites)
                {
                    sites.Add(Site((type, frame), (double)count / reader.Operations, (double)bytes / reader.Operations, Explain(explained, type, frame)));
                }

                sites.Sort(static (a, b) => b.BytesPerOperation.CompareTo(a.BytesPerOperation));
                var total = 0.0;
                foreach (var site in sites)
                {
                    total += site.BytesPerOperation;
                }

                var types = new List<TypeAllocations>(reader.Types.Count);
                foreach (var (type, (count, bytes, largest)) in reader.Types)
                {
                    types.Add(TypeTotal(type, (double)count / reader.Operations, (double)bytes / reader.Operations, (double)largest / reader.Operations));
                }

                types.Sort(static (a, b) => b.BytesPerOperation.CompareTo(a.BytesPerOperation));
                return new(reader.Name, reader.Operations, total, sites, types);
            }
            finally
            {
                File.Delete(etlx);
            }
        }

        /// <summary>
        /// Makes a site from its measured objects and bytes per operation. Code that allocates the same objects every
        /// operation measures within a sliver of a whole count; the sliver is the runtime batching samples across the
        /// edges of the measured iterations, so the count is rounded to the whole number and the bytes follow from the
        /// objects' average size. Other sites, such as a buffer that grows now and then, keep their measured share.
        /// </summary>
        /// <param name="key">The allocated type and the allocating method.</param>
        /// <param name="objects">The measured objects per operation.</param>
        /// <param name="bytes">The measured bytes per operation.</param>
        /// <param name="explanation">The explanation, or <see langword="null"/>.</param>
        /// <returns>The site.</returns>
        private static AllocationSite Site((string Type, string Frame) key, double objects, double bytes, ExplainedAllocation? explanation)
        {
            var whole = Math.Round(objects);
            var isWhole = whole >= 1 && Math.Abs(objects - whole) <= WholeSlack;
            return isWhole
                ? new(key.Type, key.Frame, whole, Math.Round(whole * (bytes / objects)), true, explanation)
                : new(key.Type, key.Frame, objects, bytes, false, explanation);
        }

        /// <summary>Makes a type's totals, rounding a near-whole count of objects per operation as <see cref="Site"/> does.</summary>
        /// <param name="type">The type.</param>
        /// <param name="objects">The measured objects per operation.</param>
        /// <param name="bytes">The measured bytes per operation.</param>
        /// <param name="resolution">The bytes per operation of the largest batch.</param>
        /// <returns>The totals.</returns>
        private static TypeAllocations TypeTotal(string type, double objects, double bytes, double resolution)
        {
            var whole = Math.Round(objects);
            var isWhole = whole >= 1 && Math.Abs(objects - whole) <= WholeSlack;
            return new(type, isWhole ? whole : objects, bytes, resolution, isWhole);
        }

        /// <summary>Finds the explanation of an allocation.</summary>
        /// <param name="explained">The expected allocations.</param>
        /// <param name="type">The allocated type.</param>
        /// <param name="frame">The allocating method.</param>
        /// <returns>The explanation, or <see langword="null"/>.</returns>
        private static ExplainedAllocation? Explain(List<ExplainedAllocation> explained, string type, string frame)
        {
            foreach (var entry in explained)
            {
                if (type.StartsWith(entry.Type, StringComparison.Ordinal) && frame.StartsWith(entry.Frame, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the innermost PdfViewerLite method on a stack. A benchmark method counts too: library code inlined into it
        /// allocates under its name, so leaving it out would hide that code's allocations.
        /// </summary>
        /// <param name="stack">The call stack, innermost first.</param>
        /// <returns>The method, or <see langword="null"/> when only the harness and the runtime are on the stack.</returns>
        private static string? LibraryFrame(TraceCallStack? stack)
        {
            for (var frame = stack; frame is not null; frame = frame.Caller)
            {
                var name = frame.CodeAddress.FullMethodName ?? string.Empty;
                if (name.StartsWith(LibraryPrefix, StringComparison.Ordinal) || name.StartsWith(EnginePrefix, StringComparison.Ordinal))
                {
                    return name;
                }
            }

            return null;
        }

        /// <summary>Source generated JSON for the explained allocations file.</summary>
        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
        [JsonSerializable(typeof(List<ExplainedAllocation>))]
        internal sealed partial class AuditJsonContext : JsonSerializerContext;

        /// <summary>
        /// Follows a trace in order: the benchmark's name, the measured iterations BenchmarkDotNet marks with its engine
        /// events, type names, and the allocations sampled inside those iterations.
        /// </summary>
        [DebuggerDisplay("TraceReader: {Name}, {Operations} operations")]
        internal sealed class TraceReader
        {
            /// <summary>The engine's event source.</summary>
            private const string EngineSource = "BenchmarkDotNet.EngineEventSource";

            /// <summary>The shared-context GPU probe's measured replay events.</summary>
            private const string GpuProbeSource = "PdfViewerLite.GpuProbe";

            /// <summary>The event starting a measured iteration.</summary>
            private const string ActualStart = "WorkloadActual/Start";

            /// <summary>The event ending a measured iteration.</summary>
            private const string ActualStop = "WorkloadActual/Stop";

            /// <summary>The event naming the benchmark.</summary>
            private const string BenchmarkStart = "Benchmark/Start";

            /// <summary>The type names by id.</summary>
            private readonly Dictionary<ulong, string> _typeNames = [];

            /// <summary>Whether a measured iteration is running.</summary>
            private bool _measuring;

            /// <summary>Gets the benchmark's full name, or <see langword="null"/> before it starts.</summary>
            internal string? Name { get; private set; }

            /// <summary>Gets the operations the measured iterations ran.</summary>
            internal long Operations { get; private set; }

            /// <summary>Gets the sampled allocations by type and innermost library method.</summary>
            internal Dictionary<(string Type, string Frame), (long Count, long Bytes)> Sites { get; } = [];

            /// <summary>Gets the sampled allocations by type, with the largest single batch the runtime reported.</summary>
            internal Dictionary<string, (long Count, long Bytes, long LargestBatch)> Types { get; } = [];

            /// <summary>Reads one event.</summary>
            /// <param name="data">The event.</param>
            internal void Read(TraceEvent data)
            {
                switch (data)
                {
                    case GCBulkTypeTraceData bulk:
                    {
                        for (var i = 0; i < bulk.Count; i++)
                        {
                            var value = bulk.Values(i);
                            _typeNames[value.TypeID] = value.TypeName;
                        }

                        return;
                    }

                    case GCSampledObjectAllocationTraceData sample when _measuring:
                    {
                        Record(_typeNames.GetValueOrDefault(sample.TypeID, $"type 0x{sample.TypeID:X}"), sample.CallStack(), sample.ObjectCountForTypeSample, sample.TotalSizeForTypeSample);
                        return;
                    }
                }

                if (data.ProviderName == EngineSource)
                {
                    ReadEngine(data);
                }
                else if (data.ProviderName == GpuProbeSource)
                {
                    ReadGpuProbe(data);
                }
            }

            /// <summary>Uses the GPU probe's own operation markers when no BenchmarkDotNet harness is present.</summary>
            /// <param name="data">The probe event.</param>
            private void ReadGpuProbe(TraceEvent data)
            {
                switch (data.EventName)
                {
                    case "Warm/Start":
                    {
                        Name = "PdfViewerLite.GpuProbe.WarmGpuReplay";
                        Operations += Convert.ToInt64(data.PayloadByName("operations"), CultureInfo.InvariantCulture);
                        _measuring = true;
                        break;
                    }

                    case "Phase/Start":
                    {
                        Name = $"PdfViewerLite.GpuProbe.{data.PayloadByName("phase") as string}";
                        Operations += Convert.ToInt64(data.PayloadByName("operations"), CultureInfo.InvariantCulture);
                        _measuring = true;
                        break;
                    }

                    case "Warm/Stop" or "Phase/Stop":
                    {
                        _measuring = false;
                        break;
                    }
                }
            }

            /// <summary>Follows the engine's iteration events.</summary>
            /// <param name="data">The engine event.</param>
            private void ReadEngine(TraceEvent data)
            {
                switch (data.EventName)
                {
                    case BenchmarkStart:
                    {
                        Name = data.PayloadByName("benchmarkName") as string;
                        break;
                    }

                    case ActualStart:
                    {
                        _measuring = true;
                        Operations += Convert.ToInt64(data.PayloadByName("totalOperations"), CultureInfo.InvariantCulture);
                        break;
                    }

                    case ActualStop:
                    {
                        _measuring = false;
                        break;
                    }
                }
            }

            /// <summary>Adds a sample made by library code.</summary>
            /// <param name="type">The allocated type.</param>
            /// <param name="stack">The call stack.</param>
            /// <param name="count">The objects the sample stands for.</param>
            /// <param name="bytes">The bytes the sample stands for.</param>
            private void Record(string type, TraceCallStack? stack, long count, long bytes)
            {
                if (LibraryFrame(stack) is not { } frame)
                {
                    return;
                }

                var current = Sites.GetValueOrDefault((type, frame));
                Sites[(type, frame)] = (current.Count + count, current.Bytes + bytes);
                var total = Types.GetValueOrDefault(type);
                Types[type] = (total.Count + count, total.Bytes + bytes, Math.Max(total.LargestBatch, bytes));
            }
        }

        /// <summary>What one benchmark allocates per measured operation.</summary>
        /// <param name="Name">The benchmark's full name.</param>
        /// <param name="Operations">The operations the measured iterations ran.</param>
        /// <param name="BytesPerOperation">The library bytes per operation.</param>
        /// <param name="Sites">The allocation sites, largest first; batched samples make the split between sites indicative.</param>
        /// <param name="Types">The allocations by type, largest first.</param>
        [DebuggerDisplay("{Name}: {BytesPerOperation} B/op")]
        internal sealed record BenchmarkAllocations(string Name, long Operations, double BytesPerOperation, List<AllocationSite> Sites, List<TypeAllocations> Types);

        /// <summary>What one benchmark allocates of one type per operation.</summary>
        /// <param name="Type">The allocated type.</param>
        /// <param name="ObjectsPerOperation">The objects per operation; a whole count when <paramref name="IsWhole"/>.</param>
        /// <param name="BytesPerOperation">The bytes per operation.</param>
        /// <param name="ResolutionPerOperation">
        /// The bytes per operation one batch of samples stands for: a batch reported across the start or end of the
        /// measurement moves the total by at most this much, so differences within it are not measurable.
        /// </param>
        /// <param name="IsWhole">Whether the type is allocated a whole number of times every operation.</param>
        [DebuggerDisplay("{Type}: {BytesPerOperation} B/op")]
        internal sealed record TypeAllocations(string Type, double ObjectsPerOperation, double BytesPerOperation, double ResolutionPerOperation, bool IsWhole);

        /// <summary>Allocations of one type made by one PdfViewerLite method.</summary>
        /// <param name="Type">The allocated type.</param>
        /// <param name="Frame">The innermost PdfViewerLite method on the stack.</param>
        /// <param name="ObjectsPerOperation">The objects allocated per operation.</param>
        /// <param name="BytesPerOperation">The bytes allocated per operation.</param>
        /// <param name="IsWhole">Whether the site allocates a whole number of objects every operation.</param>
        /// <param name="Explanation">The explanation, or <see langword="null"/> when unexplained.</param>
        [DebuggerDisplay("{Type} {BytesPerOperation} B/op in {Frame}")]
        internal sealed record AllocationSite(string Type, string Frame, double ObjectsPerOperation, double BytesPerOperation, bool IsWhole, ExplainedAllocation? Explanation);

        /// <summary>An allocation that is expected, with the reason it is acceptable.</summary>
        /// <param name="Type">The allocated type name, or a prefix of it.</param>
        /// <param name="Frame">The PdfViewerLite method that allocates, or a prefix of it.</param>
        /// <param name="Reason">Why the allocation is acceptable.</param>
        [DebuggerDisplay("{Type} in {Frame}")]
        internal sealed record ExplainedAllocation(string Type, string Frame, string Reason);
    }
}
