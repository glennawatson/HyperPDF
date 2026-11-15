// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.TestAssets;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Runs the viewer in a real Cocoa window. AppKit only works on a process's main thread, which a test does not have, so the test
/// starts the window run program and reads what it reports. The test skips on other operating systems.
/// </summary>
public sealed class CocoaWindowTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 20;

    /// <summary>The longest the window run may take, in seconds.</summary>
    private const double TimeoutSeconds = 180;

    /// <summary>The viewer opens a Cocoa window, copies to the pasteboard, draws a document and publishes it to VoiceOver.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewerRunsInACocoaWindow(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("Cocoa windows only run on macOS.");
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-cocoa-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var document = Path.Combine(folder, "cocoa.pdf");
            await File.WriteAllBytesAsync(document, TestPdf.CreateTextFilled(Pages, Lines), cancellationToken);
            var report = await RunAsync(document, folder, cancellationToken);

            await Assert.That(report).Contains("clipboard: ok");
            await Assert.That(report).Contains("viewer: drawn");
            await Assert.That(report).Contains("accessibility: document published");
            await Assert.That(report).Contains("result: ok");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Starts the window run program and waits for it to end.</summary>
    /// <param name="document">The document it opens.</param>
    /// <param name="folder">A folder for its settings.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>What the program wrote, ending with its verdict when it ran to the end.</returns>
    /// <exception cref="InvalidOperationException">The program could not be started.</exception>
    private static async Task<string> RunAsync(string document, string folder, CancellationToken cancellationToken)
    {
        var program = Path.Combine(AppContext.BaseDirectory, "HyperPdf.CocoaWindowRun.dll");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(program);
        start.ArgumentList.Add(document);
        start.ArgumentList.Add(folder);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The window run program did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            // A window run that hangs is ended, which closes its output, so the test can report what it wrote up to then.
            process.Kill(true);
        }

        return $"{await output}{await errors}";
    }
}
