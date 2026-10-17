#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:include ../scripts/ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Scripts;

const int usageError = 2;
const string usage = "Usage: RunTargetedTests.cs <project> <net10.0|net11.0|all> <class> [class ...] [--ready-dependencies]."
    + " Runs only the named classes in one build and test command."
    + " Use --ready-dependencies only after a successful build when referenced projects have not changed.";

if (args is ["--help"])
{
    Console.WriteLine(usage);
    return 0;
}

if (args is not [var project, var framework, .. var selections]
    || framework is not ("net10.0" or "net11.0" or "all"))
{
    Console.Error.WriteLine(usage);
    return usageError;
}

var ready = selections is [.., "--ready-dependencies"];
var classes = ready ? selections[..^1] : selections;
if (classes.Length == 0 || classes.Any(static name => !IsClassName(name)))
{
    Console.Error.WriteLine(usage);
    return usageError;
}

var root = ScriptProcess.Root();
var projectPath = Path.GetFullPath(project, root);
if (!File.Exists(projectPath) || Path.GetExtension(projectPath) != ".csproj")
{
    Console.Error.WriteLine("The project must be an existing .csproj file.");
    return usageError;
}

var segment = classes.Length == 1 ? classes[0] : string.Join('|', classes.Select(static name => $"({name})"));
var filter = $"/*/*/{segment}/*";
string[] frameworks = framework == "all" ? [] : ["--framework", framework];
string[] dependencies = ready ? ["--no-dependencies"] : [];
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var elapsed = Stopwatch.StartNew();
try
{
    var result = await ScriptProcess.RunAsync(
        ScriptProcess.Dotnet,
        [
            "test", "--project", projectPath, .. frameworks,
            "--no-restore", "-v", "q", "--nologo", .. dependencies,
            "--max-parallel-test-modules", "1",
            "--", "--treenode-filter", filter,
        ],
        Path.Combine(root, "src"),
        cancellation.Token);
    Console.WriteLine($"Selected classes: {string.Join(", ", classes)}. Elapsed: {elapsed.Elapsed.TotalSeconds:F2} s. Exit: {result}.");
    return result;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("The targeted test command was cancelled.");
    return 1;
}

/// <summary>Accepts exact class identifiers, not filter operators or wildcards.</summary>
/// <param name="name">The requested class name.</param>
/// <returns>Whether the name can be used as one exact filter selection.</returns>
static bool IsClassName(string name)
{
    if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_'))
    {
        return false;
    }

    foreach (var character in name.AsSpan(1))
    {
        if (!(char.IsLetterOrDigit(character) || character == '_'))
        {
            return false;
        }
    }

    return true;
}
