// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Platform.Cups;
using HyperPdf.Platform.Linux.DBus;

namespace HyperPdf.Platform.Linux;

/// <summary>Prints on Linux: straight to CUPS queues with chosen settings, or through the XDG portal's print dialog.</summary>
[DebuggerDisplay("LinuxPrintService: CUPS and print portal")]
public sealed class LinuxPrintService : IPrintService
{
    /// <summary>The portal, for the desktop's own dialog.</summary>
    private readonly PortalPrintService _portal = new();

    /// <inheritdoc/>
    public bool IsAvailable => _portal.IsAvailable;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken) => _portal.PrintAsync(filePath, title, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PrinterInfo> GetPrinters() => CupsPrinting.GetPrinters();

    /// <inheritdoc/>
    public Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(title);
        return Task.Run(() => CupsPrinting.Submit(filePath, title, options), cancellationToken);
    }
}
