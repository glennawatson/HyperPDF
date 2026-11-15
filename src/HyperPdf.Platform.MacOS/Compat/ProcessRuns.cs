// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Platform.MacOS.Compat;

/// <summary>
/// Runs a program and waits for it to end. .NET 11 does both in one call with <c>Process.RunAsync</c>, which also ends the program
/// when the wait is cancelled. .NET 10 starts the program and waits on its handle, and a cancelled wait leaves the program running.
/// Both throw <see cref="OperationCanceledException"/> when the wait is cancelled.
/// </summary>
internal static class ProcessRuns
{
    /// <summary>Runs a program until it ends.</summary>
    /// <param name="start">The program and its arguments.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see langword="true"/> when the program started and ran to its end.</returns>
    internal static async Task<bool> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
#if NET11_0_OR_GREATER
        var status = await Process.RunAsync(start, cancellationToken).ConfigureAwait(false);
        if (status.Canceled)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        return true;
#else
        using var process = Process.Start(start);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return true;
#endif
    }
}
