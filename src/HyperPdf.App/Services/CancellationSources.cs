// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Services;

/// <summary>Ending a <see cref="CancellationTokenSource"/> without blocking the UI thread.</summary>
internal static class CancellationSources
{
    /// <summary>Cancels callbacks without blocking the UI thread, then releases the source.</summary>
    /// <param name="source">The source, which the caller no longer uses.</param>
    /// <returns>A task.</returns>
    internal static async Task CancelAndDisposeAsync(CancellationTokenSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            await source.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            source.Dispose();
        }
    }
}
