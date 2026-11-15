// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Platform.MacOS.Compat;

namespace HyperPdf.Core.Tests.Compat;

/// <summary>The process helper runs a program to its end and stops waiting when cancelled, the same way on .NET 10 and .NET 11.</summary>
public sealed class ProcessRunsTests
{
    /// <summary>The shell every Unix system has.</summary>
    private const string Shell = "/bin/sh";

    /// <summary>The shell flag that runs the next argument as a command.</summary>
    private const string RunCommand = "-c";

    /// <summary>How long the cancelled program would run if it were left alone.</summary>
    private const string LongWait = "sleep 30";

    /// <summary>How long the program is given before the wait is cancelled.</summary>
    private static readonly TimeSpan CancelAfter = TimeSpan.FromMilliseconds(200);

    /// <summary>A program that ends is reported as run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgramThatEndsIsReportedAsRun()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("The helper belongs to the macOS platform services.");
            return;
        }

        var ran = await ProcessRuns.RunAsync(Start("exit 0"), CancellationToken.None);

        await Assert.That(ran).IsTrue();
    }

    /// <summary>Cancelling the wait throws instead of waiting for the program to end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingStopsTheWait()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("The helper belongs to the macOS platform services.");
            return;
        }

        using var cancel = new CancellationTokenSource(CancelAfter);

        var cancelled = false;
        try
        {
            _ = await ProcessRuns.RunAsync(Start(LongWait), cancel.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
    }

    /// <summary>Makes the start of a shell command.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The program and its arguments.</returns>
    private static ProcessStartInfo Start(string command)
    {
        var start = new ProcessStartInfo(Shell) { UseShellExecute = false };
        start.ArgumentList.Add(RunCommand);
        start.ArgumentList.Add(command);
        return start;
    }
}
