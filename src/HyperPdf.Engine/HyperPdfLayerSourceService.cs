// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

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
    public DocumentLayerListMode ListMode
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HyperPdfDocumentLayers.GetListMode(_owner);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentLayer> GetLayers() => HyperPdfDocumentLayers.GetLayers(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentLayerNode> GetLayerTree() => HyperPdfDocumentLayers.GetLayerTree(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyCollection<int> GetLayerIdsOnPage(int pageIndex) => HyperPdfDocumentLayers.GetLayerIdsOnPage(_owner, pageIndex);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentLayerConfiguration> GetConfigurations() => HyperPdfDocumentLayers.GetConfigurations(_owner);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetLayerVisible(int id, bool visible) => HyperPdfDocumentLayers.SetLayerVisible(
            _owner,
            id,
            visible);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SelectConfiguration(int index) => HyperPdfDocumentLayers.SelectConfiguration(_owner, index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ResetLayers() => HyperPdfDocumentLayers.ResetLayers(_owner);
}
