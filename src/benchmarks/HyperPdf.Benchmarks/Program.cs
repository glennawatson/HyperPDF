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

namespace HyperPdf.Benchmarks;

/// <summary>Runs the benchmarks.</summary>
public static class Program
{
    /// <summary>
    /// The EventPipe keywords of the allocation trace: GC events, types with their names, and every allocation sampled
    /// with its stack.
    /// </summary>
    private const ulong AllocationKeywords = 0x1UL | 0x80000UL | 0x200000UL | 0x1000000UL | 0x40000000UL | JitKeywords;

    /// <summary>
    /// The EventPipe keywords that let a trace name the source line of a frame: the loader and JIT events, and the map from
    /// each jitted method's IL offsets to its native code.
    /// </summary>
    private const ulong JitKeywords = 0x8UL | 0x10UL | 0x20UL | 0x20000UL;

    /// <summary>The argument that traces CPU samples from start-up, with the map that names source lines, instead of using BenchmarkDotNet's profiler.</summary>
    private const string CpuLinesArgument = "--cpu-lines";

    /// <summary>
    /// Entry point; pass BenchmarkDotNet arguments such as <c>--filter *</c>. By default every benchmark process is
    /// traced from start-up for allocations, so the runtime samples every allocation with its stack; the trace is
    /// written next to the results as <c>alloc-{pid}.nettrace</c> and read by <c>src/tools/AllocationAudit.cs</c>.
    /// Passing <c>--profiler EP</c> instead traces CPU samples with BenchmarkDotNet's EventPipe profiler, read by
    /// <c>src/tools/CpuAudit.cs</c>; allocations are not traced then, so they do not disturb the timing.
    /// </summary>
    /// <param name="args">The arguments; <c>--alloc</c> runs the allocation harness instead of BenchmarkDotNet.</param>
    /// <returns>The exit code: 0 for success.</returns>
    public static Task<int> Main(string[] args)
    {
        Environment.SetEnvironmentVariable("DISPLAY", null);
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        Environment.SetEnvironmentVariable("WAYLAND_SOCKET", null);
        Environment.SetEnvironmentVariable("DBUS_STARTER_ADDRESS", null);
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=/dev/null");
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", Path.Combine(Path.GetTempPath(), $"hyperpdf-benchmark-{Environment.ProcessId}"));
        return Array.IndexOf(args, "--alloc") >= 0 ? AllocationHarness.Harness.RunAsync(args, Console.Out) : Task.FromResult(RunBenchmarkDotNet(args));
    }

    /// <summary>Runs the benchmarks with BenchmarkDotNet.</summary>
    /// <param name="args">The BenchmarkDotNet arguments.</param>
    /// <returns>The exit code.</returns>
    private static int RunBenchmarkDotNet(string[] args)
    {
        var cpuLines = Array.IndexOf(args, CpuLinesArgument) >= 0;
        var benchmarkArguments = cpuLines ? args.Where(static argument => argument != CpuLinesArgument).ToArray() : args;
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArguments, CreateConfig(benchmarkArguments, cpuLines));
        return 0;
    }

    /// <summary>Applies allocation tracing, or CPU tracing with source lines, to the runtime jobs unless a CPU profiler was requested.</summary>
    /// <param name="args">The arguments.</param>
    /// <param name="cpuLines">Whether to trace CPU samples from start-up instead of allocations.</param>
    /// <returns>The configuration.</returns>
    private static ManualConfig CreateConfig(string[] args, bool cpuLines)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance);
        if (cpuLines)
        {
            _ = config.AddJob(new Job().WithEnvironmentVariables(CpuTrace(ArtifactsPath(args))).AsMutator());
        }
        else if (Array.IndexOf(args, "--profiler") < 0)
        {
            _ = config.AddJob(new Job().WithEnvironmentVariables(AllocationTrace(ArtifactsPath(args))).AsMutator());
        }

        return config;
    }

    /// <summary>
    /// Gets the environment that makes the runtime sample CPU stacks from start-up, with the JIT events that let
    /// <c>src/tools/CpuAudit.cs</c> name the source line of each sampled frame.
    /// </summary>
    /// <param name="artifacts">The results folder the traces are written to.</param>
    /// <returns>The environment variables.</returns>
    private static EnvironmentVariable[] CpuTrace(string artifacts)
    {
        _ = Directory.CreateDirectory(artifacts);
        var sampler = string.Create(CultureInfo.InvariantCulture, $"Microsoft-DotNETCore-SampleProfiler:0x0:{(int)EventLevel.Verbose}");
        var runtime = string.Create(CultureInfo.InvariantCulture, $"{ClrTraceEventParser.ProviderName}:0x{JitKeywords:X}:{(int)EventLevel.Verbose}");
        var engine = string.Create(CultureInfo.InvariantCulture, $"{EngineEventSource.SourceName}:0xFFFFFFFFFFFFFFFF:{(int)EventLevel.Verbose}");
        return
        [
            new("DOTNET_EnableEventPipe", "1"),
            new("DOTNET_EventPipeOutputPath", Path.Combine(artifacts, "cpu-{pid}.nettrace")),
            new("DOTNET_EventPipeConfig", $"{sampler},{runtime},{engine}"),
        ];
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
