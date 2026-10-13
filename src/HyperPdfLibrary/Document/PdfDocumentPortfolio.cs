// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document portfolio entries.</summary>
public static class PdfDocumentPortfolio
{
    /// <summary>The initial view of a portfolio that names none.</summary>
    private const string DefaultPortfolioView = "D";

    /// <summary>Gets a value indicating whether the catalog has a <c>/Collection</c> entry.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when the document has a portfolio collection.</returns>
    public static bool IsPortfolio(PdfDocument document) => document.Catalog.GetDictionary(KnownName.Collection) is not null;

    /// <summary>Gets the portfolio description and the embedded files it lists.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The portfolio, or <see langword="null"/> when the document has no <c>/Collection</c>.</returns>
    public static PdfPortfolio? GetPortfolio(PdfDocument document)
    {
        if (document.Catalog.GetDictionary(KnownName.Collection) is not { } collection)
        {
            return null;
        }

        var root = collection.Dict("Folders") is { } folders ? PortfolioReader.ReadFolder(folders, 0, [with(ReferenceEqualityComparer.Instance)]) : null;
        return new(
PortfolioReader.ReadSchema(document.Objects, collection.Dict("Schema")),
PortfolioReader.ReadSort(collection.Dict("Sort")),
collection.Text("D"),
collection.NameText("View") ?? PdfDocumentPortfolio.DefaultPortfolioView,
PortfolioReader.ReadColors(collection.Dict("Colors")),
root,
PdfDocumentPortfolio.ReadPortfolioItems(document));
    }

    /// <summary>Maps each embedded file to its collection item.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The items.</returns>
    private static PdfPortfolioItem[] ReadPortfolioItems(PdfDocument document)
    {
        var attachments = PdfDocumentAttachments.GetAttachments(document);
        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles), entries);
        var items = new PdfPortfolioItem[attachments.Count];
        for (var i = 0; i < items.Length; i++)
        {
            var spec = i < entries.Count ? entries[i].Value.AsDictionary() : null;
            var folder = i < entries.Count ? PortfolioReader.ReadFolderId(entries[i].Key.AsStringBytes()) : null;
            items[i] = new(attachments[i].Index, attachments[i].Name, folder, PortfolioReader.ReadItemValues(document.Objects, spec?.Dict("CI")));
        }

        return items;
    }
}
