// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Declares the developer extensions a signature needs: the ISO/TS 32001 and 32002 entries, and the ETSI ESIC entries for PAdES in a file older than PDF 2.0.</summary>
internal static class PdfSigningDeclarations
{
    /// <summary>The ISO/TS 32001 extension level (SHA-3 and SHAKE256).</summary>
    private const int HashLevel = 32_001;

    /// <summary>The ISO/TS 32002 extension level (digital signature algorithms).</summary>
    private const int CurveLevel = 32_002;

    /// <summary>The ESIC level for the document security store, document timestamps and signature timestamps (ETSI EN 319 142-1, 5.6.1).</summary>
    private const int ValidationLevel = 1;

    /// <summary>The ESIC level for the ETSI.CAdES.detached sub-filter (ETSI EN 319 142-1, 5.6.1).</summary>
    private const int CadesLevel = 2;

    /// <summary>Gets the ISO/TS 32001 declaration.</summary>
    private static PdfDeveloperExtension Hash { get; } = new("ISO_", "2.0", HashLevel, "https://www.iso.org/standard/45874.html") { Revision = ":2022" };

    /// <summary>Gets the ISO/TS 32002 declaration.</summary>
    private static PdfDeveloperExtension Curve { get; } = new("ISO_", "2.0", CurveLevel, "https://www.iso.org/standard/45875.html") { Revision = ":2022" };

    /// <summary>Gets the ESIC declaration for ETSI.CAdES.detached.</summary>
    private static PdfDeveloperExtension Cades { get; } = new("ESIC", "1.7", CadesLevel, null);

    /// <summary>Gets the ESIC declaration for validation data and timestamps.</summary>
    private static PdfDeveloperExtension Validation { get; } = new("ESIC", "1.7", ValidationLevel, null);

    /// <summary>Declares what a signature needs in the open edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="format">The signature format.</param>
    /// <param name="digest">The digest.</param>
    /// <param name="ellipticCurve">Whether the key is an elliptic-curve key.</param>
    /// <param name="usesValidationData">Whether the signature comes with a timestamp, a document timestamp or a document security store.</param>
    internal static void DeclareFor(PdfDocument document, PdfSignatureFormat format, PdfDigestMethod digest, bool ellipticCurve, bool usesValidationData)
    {
        if (PdfSigningDigests.IsSha3Family(digest))
        {
            _ = PdfDocumentExtensionEditing.Declare(document, Hash);
        }

        if (ellipticCurve)
        {
            _ = PdfDocumentExtensionEditing.Declare(document, Curve);
        }

        // A PDF 2.0 file never declares the PAdES extensions: its DSS, VRI and document timestamps are part of the standard (ETSI EN 319 142-1, 5.6.2).
        if (PdfDocumentVersioning.GetVersion(document) >= PdfVersion.Pdf20)
        {
            return;
        }

        if (format == PdfSignatureFormat.CadesDetached)
        {
            _ = PdfDocumentExtensionEditing.Declare(document, Cades);
        }

        if (usesValidationData || format == PdfSignatureFormat.Rfc3161)
        {
            _ = PdfDocumentExtensionEditing.Declare(document, Validation);
        }
    }
}
