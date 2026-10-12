// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using HyperPdf.Platform.Cups;

namespace HyperPdf.Platform.MacOS;

/// <summary>
/// Prints on macOS, whose printing system is CUPS: jobs go straight to a printer's queue through libcups, and the
/// system print dialog is Preview's, opened on the file.
/// </summary>
[DebuggerDisplay("MacPrintService: macOS printing")]
public sealed class MacPrintService : IPrintService
{
    /// <summary>The AppleScript runner.</summary>
    private const string ScriptRunner = "/usr/bin/osascript";

    /// <inheritdoc/>
    public bool IsAvailable => CupsPrinting.IsAvailable;

    /// <summary>Builds the AppleScript that opens Preview's print dialog on a file.</summary>
    /// <param name="filePath">The file.</param>
    /// <returns>The script.</returns>
    public static string BuildScript(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var quoted = filePath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"tell application \"Preview\" to print POSIX file \"{quoted}\" with print dialog";
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PrinterInfo> GetPrinters() => CupsPrinting.GetPrinters();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken) =>
        Task.Run(() => CupsPrinting.Submit(filePath, title, options), cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(ScriptRunner) { UseShellExecute = false };
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(BuildScript(filePath));
        using var process = Process.Start(start);
        if (process is null)
        {
            return false;
        }

        // Preview owns the dialog from here; the person prints or cancels there.
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
