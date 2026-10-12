// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Builds one-page documents from object text for the structure reader tests. Object 1 is the catalog, 2 the page tree and 3 the page; extra objects start at 4.</summary>
internal static class StructureDocuments
{
    /// <summary>Builds the file bytes.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="pageEntries">Extra page entries.</param>
    /// <param name="extra">The objects numbered from 4.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] Build(string catalogEntries, string pageEntries, params string[] extra)
    {
        string[] header = [$"<< /Type /Catalog /Pages 2 0 R {catalogEntries} >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] {pageEntries} >>",
        ];
        return MiniPdf.Build([.. header, .. extra]);
    }

    /// <summary>Opens a document with extra catalog entries.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="extra">The objects numbered from 4.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string catalogEntries, params string[] extra) => OpenPage(catalogEntries, string.Empty, extra);

    /// <summary>Opens a document with extra catalog and page entries.</summary>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="pageEntries">Extra page entries.</param>
    /// <param name="extra">The objects numbered from 4.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument OpenPage(string catalogEntries, string pageEntries, params string[] extra) => PdfDocumentReader.Open(Build(catalogEntries, pageEntries, extra), null);

    /// <summary>Gets an object as a dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Dictionary(PdfDocument document, int number) => StoreReading.GetObject(document.Objects, new(number, 0)).AsDictionary()!;
}
