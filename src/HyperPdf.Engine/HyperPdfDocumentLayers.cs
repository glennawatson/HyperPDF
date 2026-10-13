// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentLayers over the document's owned state.</summary>
internal static class HyperPdfDocumentLayers
{
    /// <summary>Gets which layers of the tree a panel lists.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The list mode; all pages when the document is closed or has none.</returns>
    internal static DocumentLayerListMode GetListMode(HyperPdfDocument self) =>
        !self.IsDisposed && PdfDocumentLayers.GetOptionalContent(self.Document).ListMode == PdfLayerListMode.VisiblePages
            ? DocumentLayerListMode.VisiblePages
            : DocumentLayerListMode.AllPages;

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
            var layer = layers[i];
            result[i] = new(layer.Id, layer.Name, layer.IsVisible, layer.IsLocked, layer.IsPrinted, layer.IsExported, layer.ZoomMin, layer.ZoomMax);
        }

        return result;
    }

    /// <summary>Gets the layers as the file orders them.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The top-level entries; empty when the document is closed or has no layers.</returns>
    internal static IReadOnlyList<DocumentLayerNode> GetLayerTree(HyperPdfDocument self) =>
        self.IsDisposed ? [] : ConvertNodes(PdfDocumentLayers.GetOptionalContent(self.Document).Tree);

    /// <summary>Gets the object numbers of the layers a page uses.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <returns>The layers' ids; empty when the page is out of range.</returns>
    internal static IReadOnlyCollection<int> GetLayerIdsOnPage(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || (uint)pageIndex >= (uint)self.Document.PageCount
            ? []
            : PdfDocumentLayers.GetOptionalContent(self.Document).GetLayerIdsOnPage(PdfDocumentPages.GetPage(self.Document, pageIndex));
    }

    /// <summary>Gets the configurations that can be used for viewing.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The configurations; empty when the document is closed or has no layers.</returns>
    internal static IReadOnlyList<DocumentLayerConfiguration> GetConfigurations(HyperPdfDocument self)
    {
        if (self.IsDisposed || !PdfDocumentLayers.GetOptionalContent(self.Document).HasLayers)
        {
            return [];
        }

        var result = new List<DocumentLayerConfiguration>();
        foreach (var configuration in PdfDocumentLayers.GetOptionalContent(self.Document).Configurations)
        {
            if (configuration.AppliesToView)
            {
                result.Add(new(configuration.Index, configuration.Name, configuration.IsActive));
            }
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

    /// <summary>Makes another configuration the one in use.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="index">The configuration's id.</param>
    /// <returns><see langword="true"/> when the configuration exists and can be used for viewing.</returns>
    internal static bool SelectConfiguration(HyperPdfDocument self, int index) => !self.IsDisposed
        && PdfDocumentLayers.GetOptionalContent(self.Document).SelectConfiguration(index);

    /// <summary>Returns every layer to the states the file sets.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns><see langword="true"/> when something changed.</returns>
    internal static bool ResetLayers(HyperPdfDocument self) => !self.IsDisposed
        && PdfDocumentLayers.GetOptionalContent(self.Document).ResetToDefault();

    /// <summary>Converts layer order entries.</summary>
    /// <param name="nodes">The library's entries.</param>
    /// <returns>The Core entries.</returns>
    private static DocumentLayerNode[] ConvertNodes(IReadOnlyList<PdfLayerNode> nodes)
    {
        var result = new DocumentLayerNode[nodes.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(nodes[i].Name, nodes[i].LayerId, ConvertNodes(nodes[i].Children));
        }

        return result;
    }
}
