// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Pdf20;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Plain files and the helpers that encrypt them and read them back, shared by the encryption writer tests.</summary>
internal static class EncryptionTestFiles
{
    /// <summary>The text the plain file draws.</summary>
    internal const string PageText = Pdf20TestPdf.SampleText;

    /// <summary>The title the metadata file's XMP holds.</summary>
    internal const string MetadataMarker = "Secret title for the metadata test";

    /// <summary>The user password the tests use.</summary>
    internal const string UserPassword = "reader-pass";

    /// <summary>The owner password the tests use.</summary>
    internal const string OwnerPassword = "owner-pass";

    /// <summary>The page size of the metadata file, in points.</summary>
    private const int PageSize = 200;

    /// <summary>Gets the page text as bytes, to search a file for it.</summary>
    internal static ReadOnlySpan<byte> PageTextBytes => "Hello PDF 2.0"u8;

    /// <summary>Gets the metadata title as bytes, to search a file for it.</summary>
    internal static ReadOnlySpan<byte> MetadataMarkerBytes => "Secret title for the metadata test"u8;

    /// <summary>Builds a one-page plain file that draws <see cref="PageText"/>.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Plain() => Pdf20TestPdf.Plain();

    /// <summary>Builds a one-page file with an XMP metadata stream, whose title is <see cref="MetadataMarker"/>.</summary>
    /// <returns>The file's bytes.</returns>
    internal static byte[] WithMetadata()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Version = "1.7" };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >>");
        pdf.Content = $"BT /F1 24 Tf 10 100 Td ({PageText}) Tj ET";
        ConformancePdf.AttachXmp(
            pdf,
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\"",
            $"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{MetadataMarker}</rdf:li></rdf:Alt></dc:title>");
        return pdf.ToBytes();
    }

    /// <summary>Encrypts a file with the compact writer.</summary>
    /// <param name="plain">The file; it may already be encrypted when the document is opened with <paramref name="openPassword"/>.</param>
    /// <param name="encryption">How to encrypt.</param>
    /// <param name="objectStreams">Whether the output uses object streams and a cross-reference stream.</param>
    /// <param name="openPassword">The password the source file opens with, or <see langword="null"/>.</param>
    /// <returns>The encrypted file's bytes.</returns>
    internal static byte[] Encrypt(byte[] plain, PdfEncryptionOptions encryption, bool objectStreams, string? openPassword)
    {
        using var store = StoreOpening.Open(plain, openPassword);
        return PdfCompactWriter.Save(store, new(objectStreams, false) { Encryption = encryption });
    }

    /// <summary>Encrypts a plain file with the compact writer.</summary>
    /// <param name="plain">The plain file.</param>
    /// <param name="encryption">How to encrypt.</param>
    /// <param name="objectStreams">Whether the output uses object streams and a cross-reference stream.</param>
    /// <returns>The encrypted file's bytes.</returns>
    internal static byte[] Encrypt(byte[] plain, PdfEncryptionOptions encryption, bool objectStreams) => Encrypt(plain, encryption, objectStreams, null);

    /// <summary>Opens a file and reads the text of its first page.</summary>
    /// <param name="file">The file.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    internal static string ReadText(byte[] file, string? password)
    {
        using var document = PdfDocumentReader.Open(file, password);
        return PdfDocumentText.GetTextPage(document, 0).Text;
    }

    /// <summary>Opens a file's objects and reads the security handler's values.</summary>
    /// <param name="file">The file.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The revision, permission word, role and metadata flag.</returns>
    internal static OpenedProtection Inspect(byte[] file, string? password)
    {
        using var store = StoreOpening.Open(file, password);
        var security = store.Security!;
        return new(security.Revision, security.Permissions, security.Role, security.EncryptMetadata);
    }
}
