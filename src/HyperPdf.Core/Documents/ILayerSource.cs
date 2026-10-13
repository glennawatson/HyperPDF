// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Lists a document's layers and shows or hides them for viewing. Safe to call from any thread.</summary>
public interface ILayerSource : IDocumentFeature
{
    /// <summary>Gets which layers of the tree a panel lists.</summary>
    DocumentLayerListMode ListMode { get; }

    /// <summary>Gets the layers with their current visibility; empty when the document has none.</summary>
    /// <returns>The layers.</returns>
    IReadOnlyList<DocumentLayer> GetLayers();

    /// <summary>Gets the layers as the file orders them, with labels that group related layers; empty when the document has none.</summary>
    /// <returns>The top-level entries.</returns>
    IReadOnlyList<DocumentLayerNode> GetLayerTree();

    /// <summary>Gets the object numbers of the layers a page uses.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    /// <returns>The layers' ids; empty when the page is out of range.</returns>
    IReadOnlyCollection<int> GetLayerIdsOnPage(int pageIndex);

    /// <summary>Gets the configurations the file offers for viewing, with the one in use marked; empty when the document has none.</summary>
    /// <returns>The configurations.</returns>
    IReadOnlyList<DocumentLayerConfiguration> GetConfigurations();

    /// <summary>Shows or hides a layer. Only how pages look changes; the file is not modified. A locked layer does not change.</summary>
    /// <param name="id">The layer's id.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the change applied.</returns>
    bool SetLayerVisible(int id, bool visible);

    /// <summary>Makes another configuration the one in use, which sets every layer to that configuration's state.</summary>
    /// <param name="index">The configuration's <see cref="DocumentLayerConfiguration.Index"/>.</param>
    /// <returns><see langword="true"/> when the configuration exists and can be used for viewing.</returns>
    bool SelectConfiguration(int index);

    /// <summary>Undoes every show and hide, returning to the states the file sets for the configuration in use.</summary>
    /// <returns><see langword="true"/> when something changed.</returns>
    bool ResetLayers();
}
