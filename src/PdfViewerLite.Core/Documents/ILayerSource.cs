// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>Lists a document's layers and shows or hides them for viewing. Safe to call from any thread.</summary>
public interface ILayerSource : IDocumentFeature
{
    /// <summary>Gets the layers with their current visibility; empty when the document has none.</summary>
    /// <returns>The layers.</returns>
    IReadOnlyList<DocumentLayer> GetLayers();

    /// <summary>Shows or hides a layer. Only how pages look changes; the file is not modified.</summary>
    /// <param name="id">The layer's id.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the change applied.</returns>
    bool SetLayerVisible(int id, bool visible);
}
