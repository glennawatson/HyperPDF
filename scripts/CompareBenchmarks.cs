#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;
using ComparisonRun = HyperPdf.Scripts.CompareBenchmarks.ComparisonRun;

const string SingleIteration = "1";

const string Dotnet = "dotnet";

const string RunCommand = "run";

const string ArgumentSeparator = "--";

const int MinimumArguments = 2;

if (args.Length < MinimumArguments || args[0] == "--help")
{
    Console.WriteLine("dotnet run --file scripts/CompareBenchmarks.cs -- <baseline ref> <filter> [more filters] [--leeway percent]");
    return args.Contains("--help") ? 0 : MinimumArguments;
}

var root = ScriptProcess.Root();

var framework = Environment.GetEnvironmentVariable("BENCHMARK_FRAMEWORK") ?? "net11.0";

if (framework is not ("net10.0" or "net11.0"))
{
    throw new ArgumentException("BENCHMARK_FRAMEWORK must be net10.0 or net11.0.");
}

var leewayIndex = Array.IndexOf(args, "--leeway");

if (leewayIndex >= 0 && (leewayIndex < MinimumArguments || leewayIndex + MinimumArguments != args.Length))
{
    throw new ArgumentException("--leeway requires a trailing percentage after at least one benchmark filter.");
}

var leeway = leewayIndex < 0 ? "5" : args[leewayIndex + 1];

var filters = args[1..(leewayIndex < 0 ? args.Length : leewayIndex)];

var output = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".cache",
    "hyperpdf",
    "benchmark-comparisons",
    Guid.NewGuid().ToString("N"));

var baseline = Path.Combine(output, "baseline");

_ = Directory.CreateDirectory(output);

Console.WriteLine($"Comparison evidence: {output}");

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var token = cancellation.Token;

var added = await ScriptProcess.RunAsync("git", ["worktree", "add", "--detach", baseline, args[0]], root, token);

if (added != 0)
{
    return added;
}

try
{
    var relative = Path.Combine("benchmarks", "HyperPdf.Benchmarks");
    CopyBenchmarks(Path.Combine(root, relative), Path.Combine(baseline, relative));
    string[] allocation = [
        "--launchCount",
        SingleIteration,
        "--iterationTime",
        "1000",
        "--iterationCount",
        SingleIteration,
        "--warmupCount",
        SingleIteration];
    string[] cpu = ["--profiler", "EP"];
    foreach (var run in new[]
    {
        new ComparisonRun(baseline, "baseline-alloc", allocation),
        new ComparisonRun(root, "change-alloc", allocation),
        new ComparisonRun(baseline, "baseline-cpu-1", cpu),
        new ComparisonRun(root, "change-cpu-1", cpu),
        new ComparisonRun(root, "change-cpu-2", cpu),
        new ComparisonRun(baseline, "baseline-cpu-2", cpu),
    }

    )
    {
        var status = await ScriptProcess.RunAsync(
            Dotnet,
            [
                RunCommand,
                "-c",
                "Release",
                "-f",
                framework,
                "--project",
                Path.Combine(run.Root, relative),
                ArgumentSeparator,
                "--filter",
                ..filters,
                "--artifacts",
                Path.Combine(output, run.Name),
                ..run.Arguments],
            run.Root,
            token);
        if (status != 0)
        {
            return status;
        }

        var reports = Directory.GetFiles(Path.Combine(output, run.Name), "*-report.csv", SearchOption.AllDirectories);
        if (reports.Length == 0 || System.Array.Exists(reports, static file => File.ReadAllText(file).Contains("NA", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{run.Name} produced no valid benchmark measurements.");
        }
    }

    var audit = await ScriptProcess.RunAsync(
        Dotnet,
        [
            RunCommand,
            "--file",
            Path.Combine(root, "scripts", "AllocationAudit.cs"),
            ArgumentSeparator,
            Path.Combine(output, "change-alloc"),
            Path.Combine(root, "benchmarks", "allocations-explained.json"),
            "--baseline",
            Path.Combine(output, "baseline-alloc")],
        root,
        token);
    var cpuAudit = await ScriptProcess.RunAsync(
        Dotnet,
        [
            RunCommand,
            "--file",
            Path.Combine(root, "scripts", "CpuAudit.cs"),
            ArgumentSeparator,
            $"{output}/change-cpu-1,{output}/change-cpu-2",
            "--baseline",
            $"{output}/baseline-cpu-1,{output}/baseline-cpu-2",
            "--leeway",
            leeway],
        root,
        token);
    return audit == 0 ? cpuAudit : audit;
}
finally
{
    await ScriptProcess.RunAsync("git", ["worktree", "remove", "--force", baseline], root, CancellationToken.None);
}

static void CopyBenchmarks(string source, string destination)
{
    _ = Directory.CreateDirectory(destination);
    foreach (var file in Directory.EnumerateFiles(source))
    {
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
    }

    foreach (var directory in Directory.EnumerateDirectories(source))
    {
        var name = Path.GetFileName(directory);
        if (name is "bin" or "obj" || name.StartsWith("BenchmarkDotNet.", StringComparison.Ordinal))
        {
            continue;
        }

        CopyBenchmarks(directory, Path.Combine(destination, name));
    }
}

namespace HyperPdf.Scripts
{
    /// <summary>Holds the comparison run settings.</summary>
    internal static class CompareBenchmarks
    {
        /// <summary>Runs the same workload in a checkout and records its evidence directory.</summary>
        /// <param name="Root">The checkout.</param>
        /// <param name="Name">The evidence directory name.</param>
        /// <param name="Arguments">The measurement settings.</param>
        internal sealed record ComparisonRun(string Root, string Name, string[] Arguments);
    }
}
