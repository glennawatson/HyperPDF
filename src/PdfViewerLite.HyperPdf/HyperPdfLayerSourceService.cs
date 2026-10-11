// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides ILayerSource through the owning document.</summary>
internal sealed class HyperPdfLayerSourceService : ILayerSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfLayerSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfLayerSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentLayer> GetLayers() => HyperPdfDocumentLayers.GetLayers(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetLayerVisible(int id, bool visible) => HyperPdfDocumentLayers.SetLayerVisible(
            _owner,
            id,
            visible);
}
