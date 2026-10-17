#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

// Build once, measure all requested filters in one process, then explain the trace.
var toolRoot = ScriptProcess.Root();

if (args.Contains("--help"))
{
    Console.WriteLine(
        "dotnet run --file scripts/AuditAllocations.cs -- [--root checkout] [benchmark filters]\nBENCHMARK_FRAMEWORK selects net11.0 (default) or net10.0.");
    return 0;
}

using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var token = cancellation.Token;

var root = toolRoot;

var filters = new List<string>();

for (var index = 0; index < args.Length; index++)
{
    if (args[index] == "--root")
    {
        index++;
        if (index == args.Length)
        {
            throw new ArgumentException("--root requires a checkout path.");
        }

        root = Path.GetFullPath(args[index]);
    }
    else if (args[index] != "--filter")
    {
        filters.Add(args[index]);
    }
}

var framework = Environment.GetEnvironmentVariable("BENCHMARK_FRAMEWORK") ?? "net11.0";

if (framework is not ("net10.0" or "net11.0"))
{
    throw new ArgumentException("BENCHMARK_FRAMEWORK must be net10.0 or net11.0.");
}

var project = Path.Combine(root, "benchmarks", "HyperPdf.Benchmarks");

var output = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".cache",
    "hyperpdf",
    "allocation-audits",
    Guid.NewGuid().ToString("N"));

_ = Directory.CreateDirectory(output);

Console.WriteLine($"Allocation evidence: {output}");

var build = await ScriptProcess.RunAsync(
    ScriptProcess.Dotnet,
    [
        "build",
        Path.Combine(project, "HyperPdf.Benchmarks.csproj"),
        "-c",
        "Release",
        "-f",
        framework,
        "-m:1",
        "/nr:false",
        "/p:UseSharedCompilation=false",
        "-v",
        "q",
        "-clp:ErrorsOnly",
        "--nologo"],
    root,
    token);

if (build != 0)
{
    return build;
}

if (filters.Count == 0)
{
    filters.Add("*");
}

var environment = new Dictionary<string, string?>
{
    ["DOTNET_EnableEventPipe"] = "1",
    ["DOTNET_EventPipeOutputPath"] = Path.Combine(output, "alloc-{pid}.nettrace"),
    ["DOTNET_EventPipeConfig"] = "Microsoft-Windows-DotNETRuntime:0x41280001:5,HyperPdf.AllocationHarness:0xFFFFFFFFFFFFFFFF:5",
};

var harness = await ScriptProcess.RunAsync(
    ScriptProcess.Dotnet,
    [
        Path.Combine(project, "bin", "Release", framework, "HyperPdf.Benchmarks.dll"),
        "--alloc",
        "--filter",
        ..filters],
    root,
    token,
    environment);

if (Directory.GetFiles(output, "*.nettrace").Length == 0)
{
    Console.Error.WriteLine("No benchmark produced an allocation trace; nothing was measured.");
    return 1;
}

var audit = await ScriptProcess.RunAsync(
    ScriptProcess.Dotnet,
    [
        "run",
        "--file",
        Path.Combine(toolRoot, "scripts", "AllocationAudit.cs"),
        "--",
        output,
        Path.Combine(root, "benchmarks", "allocations-explained.json")],
    root,
    token);

return harness == 0 ? audit : harness;
