// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Features;

/// <summary>Reads the PDF/VT claim and checks the document part structure that variable-data printing relies on.</summary>
public static class PdfVtReader
{
    /// <summary>The Info key that carries the PDF/VT version.</summary>
    private const string VersionKey = "GTS_PDFVTVersion";

    /// <summary>Reads the PDF/VT claim and the record structure of a document. Nothing is changed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfVtReport Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var claim = document.Objects.Trailer.Dict("Info")?.Text(VersionKey);
        claim = string.IsNullOrWhiteSpace(claim) ? null : claim.Trim();
        var flavor = Classify(claim);
        var parts = PdfDocumentWebCapture.GetDocumentParts(document);
        var findings = new List<string>();
        var records = new List<PdfVtRecord>();
        if (parts?.Root is null)
        {
            if (claim is not null)
            {
                findings.Add("The file claims PDF/VT but has no document part hierarchy (/DPartRoot), so it has no records.");
            }

            return new(claim, flavor, 0, parts?.NodeNames ?? [], [], [.. findings]);
        }

        if (parts.RecordLevel < 0)
        {
            findings.Add("The record level (/RecordLevel) is negative, so no level can hold records.");
        }
        else
        {
            Collect(parts.Root, 0, parts.RecordLevel, records);
        }

        Check(records, document.PageCount, parts.RecordLevel, findings);
        return new(claim, flavor, parts.RecordLevel, parts.NodeNames, [.. records], [.. findings]);
    }

    /// <summary>Recognises the flavor named by a <c>GTS_PDFVTVersion</c> value.</summary>
    /// <param name="claim">The raw value, or <see langword="null"/>.</param>
    /// <returns>The flavor.</returns>
    internal static PdfVtFlavor Classify(string? claim)
    {
        if (claim is null)
        {
            return PdfVtFlavor.None;
        }

        if (claim.Equals("PDF/VT-1", StringComparison.OrdinalIgnoreCase))
        {
            return PdfVtFlavor.Vt1;
        }

        if (claim.Equals("PDF/VT-2", StringComparison.OrdinalIgnoreCase))
        {
            return PdfVtFlavor.Vt2;
        }

        return claim.Equals("PDF/VT-2s", StringComparison.OrdinalIgnoreCase) ? PdfVtFlavor.Vt2s : PdfVtFlavor.Unknown;
    }

    /// <summary>Adds the nodes found at the record level.</summary>
    /// <param name="node">The node being visited.</param>
    /// <param name="depth">The node's depth; the root node is depth zero.</param>
    /// <param name="recordLevel">The depth that holds records.</param>
    /// <param name="records">The result list.</param>
    private static void Collect(PdfDocumentPartNode node, int depth, int recordLevel, List<PdfVtRecord> records)
    {
        if (depth == recordLevel)
        {
            records.Add(new(node.StartPage, node.EndPage, node.Metadata));
            return;
        }

        foreach (var child in node.Children)
        {
            Collect(child, depth + 1, recordLevel, records);
        }
    }

    /// <summary>Checks that the records are present, hold ordered page ranges, and between them cover every page once.</summary>
    /// <param name="records">The records.</param>
    /// <param name="pageCount">The document's page count.</param>
    /// <param name="recordLevel">The record level.</param>
    /// <param name="findings">Receives the problems.</param>
    private static void Check(List<PdfVtRecord> records, int pageCount, int recordLevel, List<string> findings)
    {
        if (records.Count == 0)
        {
            findings.Add($"The hierarchy has no nodes at record level {recordLevel}, so the file has no records.");
            return;
        }

        var next = 0;
        for (var i = 0; i < records.Count; i++)
        {
            var (start, end, _) = records[i];
            if (start is not { } first || end is not { } last || last < first)
            {
                findings.Add($"Record {i + 1} has no valid page range.");
                continue;
            }

            if (first < next)
            {
                findings.Add($"Record {i + 1} starts at page {first + 1}, which an earlier record already holds or comes before it.");
            }
            else if (first > next)
            {
                findings.Add($"Pages {next + 1} to {first} belong to no record.");
            }

            next = Math.Max(next, last + 1);
        }

        if (next > pageCount)
        {
            findings.Add($"A record ends beyond the last page ({pageCount}).");
        }
        else if (next < pageCount)
        {
            findings.Add($"Pages {next + 1} to {pageCount} belong to no record.");
        }
    }
}
