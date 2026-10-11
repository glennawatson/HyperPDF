// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;
using System.Globalization;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace PdfViewerLite.Benchmarks;

/// <summary>Runs the benchmarks.</summary>
public static class Program
{
    /// <summary>
    /// The EventPipe keywords of the allocation trace: GC events, types with their names, and every allocation sampled
    /// with its stack.
    /// </summary>
    private const ulong AllocationKeywords = 0x1UL | 0x80000UL | 0x200000UL | 0x1000000UL | 0x40000000UL;

    /// <summary>
    /// Entry point; pass BenchmarkDotNet arguments such as <c>--filter *</c>. By default every benchmark process is
    /// traced from start-up for allocations, so the runtime samples every allocation with its stack; the trace is
    /// written next to the results as <c>alloc-{pid}.nettrace</c> and read by <c>scripts/AllocationAudit.cs</c>.
    /// Passing <c>--profiler EP</c> instead traces CPU samples with BenchmarkDotNet's EventPipe profiler, read by
    /// <c>scripts/CpuAudit.cs</c>; allocations are not traced then, so they do not disturb the timing.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable("DISPLAY", null);
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        Environment.SetEnvironmentVariable("WAYLAND_SOCKET", null);
        Environment.SetEnvironmentVariable("DBUS_STARTER_ADDRESS", null);
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=/dev/null");
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", Path.Combine(Path.GetTempPath(), $"pdfviewerlite-benchmark-{Environment.ProcessId}"));
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, CreateConfig(args));
    }

    /// <summary>Creates the configuration: the default jobs, traced for allocations unless a profiler was asked for.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The configuration.</returns>
    private static ManualConfig CreateConfig(string[] args)
    {
        // The project targets several frameworks; each run builds the benchmarks for the runtime it was started on.
        var targetFramework = string.Create(CultureInfo.InvariantCulture, $"/p:TargetFramework=net{Environment.Version.Major}.0");
        var job = Job.Default.WithArguments([new MsBuildArgument(targetFramework)]);
        if (Array.IndexOf(args, "--profiler") < 0)
        {
            job = job.WithEnvironmentVariables(AllocationTrace(ArtifactsPath(args)));
        }

        return DefaultConfig.Instance.AddJob(job.AsMutator());
    }

    /// <summary>
    /// Gets the environment that makes the runtime trace allocations from start-up. Allocation sampling only takes
    /// effect when it is on before the runtime picks its allocation helpers, which a session attached later misses.
    /// </summary>
    /// <param name="artifacts">The results folder the traces are written to.</param>
    /// <returns>The environment variables.</returns>
    private static EnvironmentVariable[] AllocationTrace(string artifacts)
    {
        _ = Directory.CreateDirectory(artifacts);
        var runtime = string.Create(CultureInfo.InvariantCulture, $"{ClrTraceEventParser.ProviderName}:0x{AllocationKeywords:X}:{(int)EventLevel.Verbose}");
        var engine = string.Create(CultureInfo.InvariantCulture, $"{EngineEventSource.SourceName}:0xFFFFFFFFFFFFFFFF:{(int)EventLevel.Verbose}");
        return
        [
            new("DOTNET_EnableEventPipe", "1"),
            new("DOTNET_EventPipeOutputPath", Path.Combine(artifacts, "alloc-{pid}.nettrace")),
            new("DOTNET_EventPipeConfig", $"{runtime},{engine}"),
        ];
    }

    /// <summary>Gets the results folder from the arguments, or BenchmarkDotNet's default.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The full path.</returns>
    private static string ArtifactsPath(string[] args)
    {
        var at = Array.IndexOf(args, "--artifacts");
        return Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "BenchmarkDotNet.Artifacts");
    }
}
