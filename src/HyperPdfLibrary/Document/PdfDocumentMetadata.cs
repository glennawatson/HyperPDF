// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document information and XMP metadata.</summary>
public static class PdfDocumentMetadata
{
    /// <summary>Gets the document's XMP metadata (the catalog's <c>/Metadata</c> stream).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The metadata, or <see langword="null"/> when the document has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static XmpMetadata? GetXmp(PdfDocument document) => PdfDocumentMetadata.ReadXmp(document, document.Catalog);

    /// <summary>Gets a page's XMP metadata.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The metadata, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static XmpMetadata? GetXmp(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return PdfDocumentMetadata.ReadXmp(document, page.Dictionary);
    }

    /// <summary>Reads the XMP metadata of any object (a page, XObject, font, colour space or annotation dictionary, or a stream's dictionary).</summary>
    /// <param name="document">The document.</param>
    /// <param name="owner">The dictionary holding a <c>/Metadata</c> entry.</param>
    /// <returns>The metadata, or <see langword="null"/> when there is no readable metadata stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public static XmpMetadata? ReadXmp(PdfDocument document, PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var packet = owner.GetStream(KnownName.Metadata).DecodeOrEmpty();
        return packet.Length == 0 ? null : XmpParser.Parse(packet);
    }

    /// <summary>Gets the document information.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The information.</returns>
    public static PdfDocumentInfo GetInfo(PdfDocument document)
    {
        var info = document.Objects.Trailer.GetDictionary(KnownName.Info);
        return new()
        {
            Title = PdfDocumentMetadata.Text(info, KnownName.Title),
            Author = PdfDocumentMetadata.Text(info, KnownName.Author),
            Subject = PdfDocumentMetadata.Text(info, KnownName.Subject),
            Keywords = PdfDocumentMetadata.Text(info, KnownName.Keywords),
            Creator = PdfDocumentMetadata.Text(info, KnownName.Creator),
            Producer = PdfDocumentMetadata.Text(info, KnownName.Producer),
            Created = info is null ? null : PdfDate.Parse(info.GetStringBytes(KnownName.CreationDate)),
            Modified = info is null ? null : PdfDate.Parse(info.GetStringBytes(KnownName.ModDate)),
            Trapped = info?.NameText("Trapped"),
            Version = document.Catalog.GetName(KnownName.Version) is { IsNone: false } version ? document.Objects.Names.GetString(version) : document.Objects.Version,
            IsEncrypted = document.IsEncrypted,
        };
    }

    /// <summary>
    /// Gets the document information a viewer shows: the information dictionary combined with the catalog's XMP. A PDF 2.0
    /// file takes the XMP value when both exist (ISO 32000-2 §14.3.3); an earlier file uses XMP only for an entry the
    /// dictionary lacks. <see cref="GetInfo"/> still reports the dictionary alone.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The information.</returns>
    public static PdfDocumentInfo GetResolvedInfo(PdfDocument document)
    {
        var info = PdfDocumentMetadata.GetInfo(document);
        return PdfDocumentMetadata.GetXmp(document) is { } xmp ? XmpInfoResolver.Resolve(info, xmp) : info;
    }

    /// <summary>Reads an information entry, treating blank text as missing.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    private static string? Text(PdfDictionary? info, KnownName key)
    {
        var text = info?.GetText(key);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
