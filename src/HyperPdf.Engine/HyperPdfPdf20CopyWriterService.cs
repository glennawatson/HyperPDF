// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Provides IPdf20CopyWriter through the owning document.</summary>
internal sealed class HyperPdfPdf20CopyWriterService : IPdf20CopyWriter
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPdf20CopyWriterService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPdf20CopyWriterService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<Pdf20CopyResult> WritePdf20Copy(Stream destination) =>
        Signal.FromAsync(token => HyperPdfDocumentPdf20Copy.WriteAsync(_owner, destination, default, token));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<Pdf20CopyResult> WritePdf20Copy(Stream destination, Pdf20CopyOptions options) =>
        Signal.FromAsync(token => HyperPdfDocumentPdf20Copy.WriteAsync(_owner, destination, options, token));
}
