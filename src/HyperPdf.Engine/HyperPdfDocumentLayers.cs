// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentLayers over the document's owned state.</summary>
internal static class HyperPdfDocumentLayers
{
    /// <summary>Gets the layers with their current visibility; empty when the document has none.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The layers.</returns>
    internal static IReadOnlyList<DocumentLayer> GetLayers(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return [];
        }

        var layers = PdfDocumentLayers.GetOptionalContent(self.Document).Layers;
        var result = new DocumentLayer[layers.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(layers[i].Id, layers[i].Name, layers[i].IsVisible);
        }

        return result;
    }

    /// <summary>Shows or hides a layer. Only how pages look changes; the file is not modified.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="id">The layer's id.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the change applied.</returns>
    internal static bool SetLayerVisible(HyperPdfDocument self, int id, bool visible) => !self.IsDisposed
        && PdfDocumentLayers.GetOptionalContent(self.Document).SetVisible(id, visible);
}
