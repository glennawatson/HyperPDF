#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:include ScriptProcess.cs

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Scripts;

const string usage = "Usage: RunBoundedTests.cs <project or solution> <net10.0, net11.0 or all> <results> <test timeout, e.g. 25m> <process timeout, e.g. 00:28:00>."
    + " Build the target first. Solution diagnostics stay in the test hosts' default TestResults directories.";

const int usageError = 2;

const int timeoutExitCode = 3;

const int cleanupSeconds = 30;

if (args is ["--help"])
{
    Console.WriteLine(usage);
    return 0;
}

if (args is not [var project, var framework, var results, var testTimeout, var processTimeout]
    || !TimeSpan.TryParse(processTimeout, CultureInfo.InvariantCulture, out var maximum)
    || maximum <= TimeSpan.Zero
    || framework is not ("net10.0" or "net11.0" or "all"))
{
    Console.Error.WriteLine(usage);
    return usageError;
}

var root = ScriptProcess.Root();

var projectPath = Path.GetFullPath(project, root);

var output = Path.GetFullPath(results, root);

_ = Directory.CreateDirectory(output);

var isSolution = Path.GetExtension(projectPath) is ".slnx" or ".sln" or ".slnf";

string[] targetArguments = [isSolution ? "--solution" : "--project", projectPath];

string[] frameworkArguments = framework is "all" ? [] : ["--framework", framework];

// Keep the test hosts' default result paths, matching the shared solution job.
string[] resultArguments = isSolution ? [] : ["--results-directory", output, "--diagnostic-output-directory", output];

var diagnosticLocation = isSolution ? root : output;

using var deadline = new CancellationTokenSource(maximum);

using var interrupted = new CancellationTokenSource();

using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, interrupted.Token);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    interrupted.Cancel();
};

using var process = ScriptProcess.Start(
    ScriptProcess.Dotnet,
    [
        "test", .. targetArguments,
        "--configuration", "Release", .. frameworkArguments,
        "--no-restore", "--no-dependencies",
        .. resultArguments,
        "--", "--timeout", testTimeout, "--diagnostic", "--diagnostic-verbosity", "Trace", "--diagnostic-synchronous-write",
        "--log-level", "Trace", "--output", "Detailed", "--coverage", "--coverage-output-format", "cobertura",
    ],
    Path.Combine(root, "src"));

try
{
    await process.WaitForExitAsync(cancellation.Token);
    var status = await process.WaitForExitStatusAsync(CancellationToken.None);
    return status is { Signal: null, ExitCode: int code } ? code : 1;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    if (!process.HasExited)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The child can exit between the state check and the kill request.
        }
    }

    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(cleanupSeconds));
    await process.WaitForExitAsync(cleanup.Token);
    Console.Error.WriteLine(deadline.IsCancellationRequested
        ? $"The test command exceeded its process deadline of {maximum}. Its process tree was stopped. Diagnostic files remain in {diagnosticLocation}."
        : $"The test command was cancelled and its process tree was stopped. Diagnostic files remain in {diagnosticLocation}.");
    return timeoutExitCode;
}
