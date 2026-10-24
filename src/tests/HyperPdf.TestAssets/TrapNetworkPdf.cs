// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.TestAssets;

/// <summary>
/// Builds two-page PDF 1.7 files whose first page carries a trap network annotation (ISO 32000-1 §14.11.6). Page 1 draws
/// text in <see cref="FontObject"/> and the form <see cref="FormObject"/>, whose own resources hold the graphics state
/// <see cref="GraphicsStateObject"/>; its annotations are a square without <c>/AS</c>, a stamp whose <c>/AS</c> is
/// <c>/On</c>, and the trap network. Page 2 has text only.
/// </summary>
public static class TrapNetworkPdf
{
    /// <summary>The object number of page 1.</summary>
    public static readonly int FirstPageObject = 3;

    /// <summary>The object number of page 1's content stream.</summary>
    public static readonly int ContentObject = 4;

    /// <summary>The object number of the font both pages use.</summary>
    public static readonly int FontObject = 5;

    /// <summary>The object number of the form XObject page 1 draws.</summary>
    public static readonly int FormObject = 6;

    /// <summary>The object number of the graphics state in the form's resources.</summary>
    public static readonly int GraphicsStateObject = 7;

    /// <summary>The object number of the square annotation, which has no <c>/AS</c>.</summary>
    public static readonly int SquareObject = 8;

    /// <summary>The object number of the stamp annotation, whose <c>/AS</c> is <c>/On</c>.</summary>
    public static readonly int StampObject = 9;

    /// <summary>The object number of the trap network annotation.</summary>
    public static readonly int NetworkObject = 10;

    /// <summary>The object number of page 2's content stream.</summary>
    public static readonly int SecondContentObject = 15;

    /// <summary>The appearance and flags of a conforming network: Print and ReadOnly, and one appearance named by <c>/AS</c>.</summary>
    public static readonly string Appearance = "/F 68 /AP << /N << /Default 11 0 R >> >> /AS /Default";

    /// <summary>The <c>/Version</c> and <c>/AnnotStates</c> entries that match page 1.</summary>
    public static readonly string Tracking = "/Version [4 0 R 5 0 R 6 0 R 7 0 R] /AnnotStates [null /On]";

    /// <summary>The date the dated samples give the trap network.</summary>
    public static readonly string NetworkDate = "D:20200601000000Z";

    /// <summary>The entries of page 1's resource dictionary.</summary>
    private const string PageResources = "/Font << /F1 5 0 R >> /XObject << /Fm1 6 0 R >> /ProcSet [/PDF /Text]";

    /// <summary>Gets a conforming network tracked by <c>/Version</c> and <c>/AnnotStates</c>.</summary>
    public static TrapNetworkSample Versioned => new($"{Appearance} {Tracking}", string.Empty, false, false);

    /// <summary>Creates a conforming network tracked by <c>/LastModified</c>.</summary>
    /// <param name="pageDate">The page's <c>/LastModified</c>, such as <c>D:20190101000000Z</c>, or empty for none.</param>
    /// <returns>The sample.</returns>
    public static TrapNetworkSample Dated(string pageDate)
    {
        ArgumentNullException.ThrowIfNull(pageDate);
        var page = pageDate.Length == 0 ? string.Empty : $"/LastModified ({pageDate})";
        return new($"{Appearance} /LastModified ({NetworkDate})", page, false, false);
    }

    /// <summary>
    /// Builds the file for a sample. Objects 1 and 2 are the catalog and page tree, 11 is the traps' appearance, 12 is
    /// page 2, 13 is a second trap network listed only when the sample asks for two, and 14 is the stamp's appearance.
    /// </summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The file bytes.</returns>
    public static byte[] Build(TrapNetworkSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var annotations = sample.TwoNetworks ? "8 0 R 13 0 R 9 0 R 10 0 R" : "8 0 R 9 0 R 10 0 R";
        if (sample.NetworkFirst)
        {
            annotations = "10 0 R 8 0 R 9 0 R";
        }

        var network = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /TrapNet /Rect [0 0 612 792] {sample.Network} >>");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 12 0 R] /Count 2 >>",
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << {PageResources} >> /Contents 4 0 R /Annots [{annotations}] {sample.PageEntries} >>"),
            MiniPdf.Stream(string.Empty, "BT /F1 24 Tf 72 700 Td (Trapped page) Tj ET /Fm1 Do"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << /ExtGState << /G1 7 0 R >> >>", "/G1 gs 0 0 1 rg 72 400 100 100 re f"),
            "<< /Type /ExtGState /CA 1 /ca 1 >>",
            "<< /Type /Annot /Subtype /Square /Rect [300 300 400 400] /F 4 /C [1 0 0] /Contents (A square) >>",
            "<< /Type /Annot /Subtype /Stamp /Rect [300 500 400 550] /F 4 /AS /On /AP << /N << /On 14 0 R /Off 14 0 R >> >> >>",
            network,
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 612 792] /PCM /DeviceCMYK", "0 0 0 1 k 72 399 102 2 re f"),
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 15 0 R >>",
            network,
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 100 50]", "0 1 0 rg 0 0 100 50 re f"),
            MiniPdf.Stream(string.Empty, "BT /F1 24 Tf 72 700 Td (Second page) Tj ET"));
    }
}
