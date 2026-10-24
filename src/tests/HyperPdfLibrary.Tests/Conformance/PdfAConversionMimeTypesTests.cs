// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks span-based filename suffix matching for embedded-file media types.</summary>
public sealed class PdfAConversionMimeTypesTests
{
    /// <summary>Every supported extension retains its media type regardless of letter case.</summary>
    /// <param name="name">The filename.</param>
    /// <param name="expected">The expected media type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("file.PDF", "application/pdf")]
    [Arguments("file.XML", "application/xml")]
    [Arguments("file.JSON", "application/json")]
    [Arguments("file.TXT", "text/plain")]
    [Arguments("file.CSV", "text/csv")]
    [Arguments("file.HTM", "text/html")]
    [Arguments("file.HTML", "text/html")]
    [Arguments("file.PNG", "image/png")]
    [Arguments("file.JPG", "image/jpeg")]
    [Arguments("file.JPEG", "image/jpeg")]
    [Arguments("file.GIF", "image/gif")]
    [Arguments("file.TIF", "image/tiff")]
    [Arguments("file.TIFF", "image/tiff")]
    [Arguments("file.ZIP", "application/zip")]
    [Arguments("file.DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [Arguments("file.XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [Arguments("file.ODT", "application/vnd.oasis.opendocument.text")]
    public async Task Guess_KnownExtension_PreservesType(string name, string expected)
    {
        await Assert.That(PdfAConversionMimeTypes.Guess(name)).IsEqualTo(expected);
        await Assert.That(PdfAConversionMimeTypes.Guess(name.ToLowerInvariant())).IsEqualTo(expected);
    }

    /// <summary>Unknown suffixes, missing suffixes and dotted folders do not imply a media type.</summary>
    /// <param name="name">The filename.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("file")]
    [Arguments("file.")]
    [Arguments("folder.pdf/file")]
    [Arguments("file.pdf/")]
    [Arguments("file.unknown")]
    [Arguments("file.PＤF")]
    public async Task Guess_UnknownExtension_UsesBinaryFallback(string name) =>
        await Assert.That(PdfAConversionMimeTypes.Guess(name)).IsEqualTo("application/octet-stream");
}
