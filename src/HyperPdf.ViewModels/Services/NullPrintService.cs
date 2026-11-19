// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;

namespace HyperPdf.ViewModels.Services;

/// <summary>A print service for platforms without one.</summary>
[DebuggerDisplay("NullPrintService")]
public sealed class NullPrintService : IPrintService
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<bool> Print(string filePath, string title) => Signal.Return(false);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<PrinterInfo> GetPrinters() => [];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<PrintOutcome> Submit(string filePath, string title, PrintJobOptions options) =>
        Signal.Return(new PrintOutcome(false, UiText.Get("Printing is not available on this desktop.")));
}
