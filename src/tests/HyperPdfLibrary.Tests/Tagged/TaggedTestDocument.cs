// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Opens tagged test documents with their own font loader.</summary>
internal static class TaggedTestDocument
{
    /// <summary>Opens PDF bytes with the tagged test font.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <returns>The opened document.</returns>
    internal static PdfDocument Open(byte[] bytes) => PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions
    {
        FontFactory = static dictionary => IsTestFont(dictionary) ? new TaggedTestFont(dictionary) : PdfFontLoader.Load(dictionary),
    });

    /// <summary>Opens PDF bytes with the tagged test font and Skia drawing services, so images are decoded and their places recorded.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <returns>The opened document.</returns>
    internal static PdfDocument OpenWithImages(byte[] bytes) => PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions
    {
        FontFactory = static dictionary => IsTestFont(dictionary) ? new TaggedTestFont(dictionary) : null,
        DrawingServices = SkiaTestRegistration.CreateServices(),
    });

    /// <summary>Determines whether a font dictionary names the test font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns><see langword="true"/> for <c>/BaseFont /TaggedTest</c>.</returns>
    private static bool IsTestFont(PdfDictionary dictionary) =>
        dictionary.Owner?.Names.NameEquals(dictionary.GetName(KnownName.BaseFont), "TaggedTest"u8) == true;
}
