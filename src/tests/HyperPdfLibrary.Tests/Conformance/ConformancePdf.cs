// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Rendering;
using static HyperPdfLibrary.Tests.Graphics.IccTestProfileBuilder;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Builds one-page PDFs that claim PDF/A conformance, with XMP packets and output intents.</summary>
internal static class ConformancePdf
{
    /// <summary>The page size in points.</summary>
    internal const int PageSize = 100;

    /// <summary>The components of a CMYK profile.</summary>
    internal const int CmykComponents = 4;

    /// <summary>The output intent type of PDF/A.</summary>
    internal const string PdfASubtype = "GTS_PDFA1";

    /// <summary>The grid points of the test profile's table.</summary>
    private const int Grid = 3;

    /// <summary>The stored value of a zero a* or b* channel.</summary>
    private const double NeutralChroma = 0.5;

    /// <summary>Gets a small CMYK profile whose lightness follows black only, so cyan, magenta and yellow draw as grey.</summary>
    /// <returns>The profile bytes.</returns>
    internal static byte[] CmykProfile() =>
        Profile(Version2, "CMYK", "Lab ", new Tag("A2B0", Lut8(CmykComponents, Grid, static input => [1 - input[CmykComponents - 1], NeutralChroma, NeutralChroma])));

    /// <summary>Starts a page with an empty content stream.</summary>
    /// <returns>The builder.</returns>
    internal static RenderTestPdf Page() => new(PageSize, PageSize);

    /// <summary>Adds an XMP packet with <c>pdfaid</c> properties to the catalog.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="part">The part, or null for none.</param>
    /// <param name="conformance">The level, or null for none.</param>
    /// <param name="revision">The revision, or null for none.</param>
    internal static void Claim(RenderTestPdf pdf, int? part, string? conformance, int? revision)
    {
        var properties = string.Empty;
        if (part is { } p)
        {
            properties += string.Create(CultureInfo.InvariantCulture, $"<pdfaid:part>{p}</pdfaid:part>");
        }

        if (conformance is not null)
        {
            properties += $"<pdfaid:conformance>{conformance}</pdfaid:conformance>";
        }

        if (revision is { } r)
        {
            properties += string.Create(CultureInfo.InvariantCulture, $"<pdfaid:rev>{r}</pdfaid:rev>");
        }

        AttachXmp(pdf, "xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\"", properties);
    }

    /// <summary>Adds an XMP packet to the catalog.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="namespaces">The namespace declarations of the description.</param>
    /// <param name="properties">The property elements.</param>
    internal static void AttachXmp(RenderTestPdf pdf, string namespaces, string properties)
    {
        var packet = "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + $"<rdf:Description rdf:about=\"\" {namespaces}>{properties}</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var number = pdf.AddStream("/Type /Metadata /Subtype /XML", packet);
        pdf.CatalogEntries += string.Create(CultureInfo.InvariantCulture, $" /Metadata {number} 0 R");
    }

    /// <summary>Adds an output intent with a profile stream to the catalog.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="subtype">The intent type, such as GTS_PDFA1.</param>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="components">The <c>/N</c> of the profile stream.</param>
    internal static void Intent(RenderTestPdf pdf, string subtype, byte[] profile, int components)
    {
        var stream = pdf.AddStream(string.Create(CultureInfo.InvariantCulture, $"/N {components}"), profile);
        pdf.CatalogEntries += string.Create(
            CultureInfo.InvariantCulture,
            $" /OutputIntents [ << /Type /OutputIntent /S /{subtype} /OutputConditionIdentifier (Test condition) "
            + $"/RegistryName (http://www.color.org) /Info (Test info) /DestOutputProfile {stream} 0 R >> ]");
    }
}
