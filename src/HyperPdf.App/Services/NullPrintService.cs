// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;

namespace HyperPdf.App.Services;

/// <summary>A print service for platforms without one.</summary>
internal sealed class NullPrintService : IPrintService
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PrinterInfo> GetPrinters() => [];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken) =>
        Task.FromResult(new PrintOutcome(false, "Printing is not available on this desktop."));
}
