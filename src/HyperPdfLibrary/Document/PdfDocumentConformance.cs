// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document conformance claims.</summary>
public static class PdfDocumentConformance
{
    /// <summary>The XMP namespace of the PDF/X identification properties.</summary>
    private const string PdfXIdNamespace = "http://www.npes.org/pdfx/ns/id/";

    /// <summary>The Info and XMP key of the PDF/X version.</summary>
    private const string PdfXVersionKey = "GTS_PDFXVersion";

    /// <summary>The component count of a gray profile.</summary>
    private const int GrayProfileComponents = 1;

    /// <summary>The component count of an RGB profile.</summary>
    private const int RgbProfileComponents = 3;

    /// <summary>The component count of a CMYK profile.</summary>
    private const int CmykProfileComponents = 4;

    /// <summary>
    /// Reads what the file claims about PDF/A, PDF/UA and PDF/X and lists the features a viewer can see without full
    /// validation. This is a reading report, not a validator: it never says that a file conforms. Use veraPDF to validate.
    /// It reads every object and the page resources on each call, so keep the result.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    public static PdfConformanceReport GetConformance(PdfDocument document)
    {
        var xmp = PdfDocumentMetadata.GetXmp(document);
        var intents = PdfDocumentCatalog.GetOutputIntents(document);
        var summaries = new PdfOutputIntentSummary[intents.Length];
        for (var i = 0; i < intents.Length; i++)
        {
            summaries[i] = PdfDocumentConformance.Summarise(intents[i]);
        }

        var claim = PdfDocumentConformance.ReadPdfAClaim(xmp);
        var observations = new ConformanceScan(document).Run(claim, intents.Length > 0);
        return new(xmp is not null, claim, xmp?.PdfUaPart, PdfDocumentConformance.ReadPdfXVersion(document, xmp), summaries, observations);
    }

    /// <summary>Reads the PDF/A claim from the <c>pdfaid</c> properties.</summary>
    /// <param name="xmp">The catalog's XMP, or null.</param>
    /// <returns>The claim, or null when the packet has no <c>pdfaid:part</c>.</returns>
    internal static PdfAClaim? ReadPdfAClaim(XmpMetadata? xmp)
    {
        if (xmp?.PdfAPart is not { } part)
        {
            return null;
        }

        var conformance = xmp.PdfAConformance?.Trim().ToUpperInvariant();
        return new(part, string.IsNullOrEmpty(conformance) ? null : conformance, xmp.PdfARevision);
    }

    /// <summary>Summarises an output intent for the report.</summary>
    /// <param name="intent">The intent.</param>
    /// <returns>The summary.</returns>
    private static PdfOutputIntentSummary Summarise(PdfOutputIntent intent) => new(
        intent.Subtype,
        intent.OutputConditionIdentifier,
        intent.RegistryName,
        intent.Info,
        intent.Profile is not null,
        intent.ComponentCount,
        intent.ComponentCount switch
    {
        PdfDocumentConformance.GrayProfileComponents => "GRAY",
        PdfDocumentConformance.RgbProfileComponents => "RGB",
        PdfDocumentConformance.CmykProfileComponents => "CMYK",
        _ => null,
    });

    /// <summary>Reads the PDF/X version from the Info dictionary, else from the XMP.</summary>
    /// <param name="document">The document.</param>
    /// <param name="xmp">The catalog's XMP, or null.</param>
    /// <returns>The version text, or null.</returns>
    private static string? ReadPdfXVersion(PdfDocument document, XmpMetadata? xmp)
    {
        var fromInfo = document.Objects.Trailer.Dict("Info")?.Text(PdfDocumentConformance.PdfXVersionKey);
        return string.IsNullOrWhiteSpace(fromInfo) ? xmp?.GetValue(PdfDocumentConformance.PdfXIdNamespace, PdfDocumentConformance.PdfXVersionKey) : fromInfo;
    }
}
