// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Refuses the documents a PDF 2.0 copy must not be made from, before any work is done: signed files, whose signatures a
/// rewrite breaks (ISO 32000-2 12.8.1); encrypted files, whose handlers PDF 2.0 deprecates and which the library cannot
/// encrypt again (7.6.1); and files that claim a standard whose header the copy would break (ISO 19005-1, -2 and -3
/// 6.1.2 need a 1.x header; ISO 15930 PDF/X-1 to X-5 need a 1.x header).
/// </summary>
internal static class Pdf20ConversionSource
{
    /// <summary>The PDF/X version text of the one edition that is based on PDF 2.0.</summary>
    private const string PdfX6 = "PDF/X-6";

    /// <summary>The standard that holds the PDF/X rules.</summary>
    private const string PdfXSpecification = "ISO 15930";

    /// <summary>Checks a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What the copy may drop, which lifts the refusals for signatures and protection.</param>
    /// <returns>The refusal, or <see langword="null"/> to go on.</returns>
    internal static Pdf20ConversionOutput? Check(PdfDocument document, in Pdf20ConversionOptions options)
    {
        var store = document.Objects;
        if (IsSigned(store) && !options.RemoveSignatures)
        {
            return Refused(
                Pdf20ConversionSession.Specification,
                "12.8.1",
                "The document is signed, and a PDF 2.0 copy would break the signatures. Remove the signatures first, then save the copy.");
        }

        if (store.Security is null)
        {
            return CheckClaims(document);
        }

        if (!options.RemoveEncryption)
        {
            return Refused(
                Pdf20ConversionSession.Specification,
                "7.6.1",
                "The document is protected with a password. PDF 2.0 deprecates the older kinds of protection, and HyperPDF cannot write the newest kind yet, "
                + "so the copy would lose its protection. Remove the protection first, then save the copy.");
        }

        // Dropping the protection is only for someone who holds the owner's rights, never a way round the owner's limits.
        return PdfProtectionAuthority.IsAuthorised(document, null)
            ? CheckClaims(document)
            : Refused(Pdf20ConversionSession.Specification, "7.6.1", "The document is protected, and only its owner can remove the protection to make a PDF 2.0 copy.");
    }

    /// <summary>
    /// Determines whether a document holds a signature a rewrite would break. A usage rights signature (the /UR and /UR3
    /// entries of /Perms that Reader extensions add) does not count: PDF 2.0 deprecates it (ISO 32000-2 12.8.2.3), any
    /// change already invalidates it, and the copy removes it.
    /// </summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when a certification signature or a filled signature field is there.</returns>
    private static bool IsSigned(PdfObjectStore store)
    {
        if (store.Catalog.GetDictionary(KnownName.Perms) is { } perms && !Pdf20ConversionCatalog.HoldsOnlyUsageRights(perms))
        {
            return true;
        }

        var fields = store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        return fields is not null && SignaturePresence.HasSignedField(fields, 0);
    }

    /// <summary>Refuses a file that claims PDF/A-1, 2 or 3, or a PDF/X edition older than PDF/X-6.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The refusal, or <see langword="null"/> to go on.</returns>
    private static Pdf20ConversionOutput? CheckClaims(PdfDocument document)
    {
        if (PdfAClaimReading.ReadClaim(document.Objects) is { Part: >= PdfAClaim.Part1 and <= PdfAClaim.Part3 } claim)
        {
            return Refused(
                SpecificationOf(claim.Part),
                "6.1.2",
                $"The document claims PDF/A-{claim.Label.ToLowerInvariant()}, which needs a PDF 1.x header, so it cannot be a PDF 2.0 file. "
                + "Use Save PDF/A Copy with PDF/A-4 to get a PDF 2.0 archive file.");
        }

        return ReadPdfX(document) is { } pdfx && !pdfx.Contains(PdfX6, StringComparison.OrdinalIgnoreCase)
            ? Refused(PdfXSpecification, string.Empty, $"The document claims {pdfx}, which needs a PDF 1.x header, so it cannot be a PDF 2.0 file. Only PDF/X-6 is based on PDF 2.0.")
            : null;
    }

    /// <summary>Reads the PDF/X version the document claims, from its XMP or its information dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The claim, such as PDF/X-4, or <see langword="null"/>.</returns>
    private static string? ReadPdfX(PdfDocument document)
    {
        if (PdfDocumentMetadata.GetXmp(document)?.PdfXVersion is { Length: > 0 } fromXmp)
        {
            return fromXmp;
        }

        var store = document.Objects;
        var info = StoreReading.Resolve(store, store.Trailer.GetRaw(KnownName.Info)).AsDictionary();
        var name = store.Names.Intern("GTS_PDFXVersion"u8);
        return info?.GetText(name) is { Length: > 0 } fromInfo ? fromInfo : null;
    }

    /// <summary>Gets the standard of a PDF/A part.</summary>
    /// <param name="part">The part, 1 to 3.</param>
    /// <returns>For example ISO 19005-2:2011.</returns>
    private static string SpecificationOf(int part) => part switch
    {
        PdfAClaim.Part1 => "ISO 19005-1:2005",
        PdfAClaim.Part2 => "ISO 19005-2:2011",
        _ => "ISO 19005-3:2012",
    };

    /// <summary>Builds a refusal with one problem.</summary>
    /// <param name="specification">The standard behind the reason.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="description">The reason, in plain words.</param>
    /// <returns>The outcome.</returns>
    private static Pdf20ConversionOutput Refused(string specification, string clause, string description) =>
        new(new([], [new(specification, clause, description)]), null);
}
