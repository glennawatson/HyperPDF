// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks a file's PDF/X claim against what the file holds. PDF/X-4 and PDF/X-4p are checked for the header version,
/// encryption, title, output intent and page boxes. Fonts, colour spaces, transparency and the other content rules are
/// listed as not checked rather than passed.
/// </summary>
public static class PdfXReader
{
    /// <summary>The newest header minor version PDF/X-4 allows (PDF 1.6).</summary>
    private const int MaxHeaderMinor = 6;

    /// <summary>The header major version PDF/X-4 allows.</summary>
    private const int HeaderMajor = 1;

    /// <summary>The output intent subtype PDF/X uses.</summary>
    private const string PdfXIntent = "GTS_PDFX";

    /// <summary>The rule name of the output intent checks.</summary>
    private const string OutputIntentRule = "OutputIntent";

    /// <summary>The requirements the report leaves unchecked.</summary>
    private static readonly string[] NotCheckedRules =
    [
        "Fonts are embedded.",
        "Colour spaces and ICC profiles match the output intent.",
        "Transparency, overprint and prohibited actions or annotations.",
        "XMP metadata matches the Info dictionary.",
    ];

    /// <summary>Reads the PDF/X claim of a document and checks it. Nothing is changed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfXReport Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var xmp = PdfDocumentMetadata.ReadOwnerXmp(document, document.Catalog, CancellationToken.None);
        var claim = PdfDocumentConformance.ReadPdfXVersion(document, xmp);
        claim = string.IsNullOrWhiteSpace(claim) ? null : claim.Trim();
        var level = Classify(claim);
        if (level is not (PdfXLevel.X4 or PdfXLevel.X4p))
        {
            string[] unsupported = level == PdfXLevel.None ? [] : ["No rule set exists yet for this PDF/X level, so nothing was checked."];
            return new(claim, level, [], unsupported);
        }

        var findings = new List<PdfXFinding> { CheckHeader(document), CheckEncryption(document), CheckTitle(document), CheckOutputIntent(document, level) };
        CheckPageBoxes(document, findings);
        return new(claim, level, [.. findings], NotCheckedRules);
    }

    /// <summary>Recognises the level a <c>GTS_PDFXVersion</c> value names.</summary>
    /// <param name="claim">The raw value, or <see langword="null"/>.</param>
    /// <returns>The level.</returns>
    internal static PdfXLevel Classify(string? claim)
    {
        if (claim is null)
        {
            return PdfXLevel.None;
        }

        var text = claim.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return text switch
        {
            "PDF/X-1A:2001" or "PDF/X-1A:2003" or "PDF/X-1:2001" => PdfXLevel.X1a,
            "PDF/X-3:2002" or "PDF/X-3:2003" => PdfXLevel.X3,
            "PDF/X-4" => PdfXLevel.X4,
            "PDF/X-4P" => PdfXLevel.X4p,
            "PDF/X-5G" or "PDF/X-5PG" or "PDF/X-5N" => PdfXLevel.X5,
            _ => PdfXLevel.Unknown,
        };
    }

    /// <summary>Checks that the header version is no newer than PDF 1.6.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckHeader(PdfDocument document)
    {
        var version = PdfDocumentVersioning.GetHeaderVersion(document);
        var passed = version.Major == HeaderMajor && version.Minor <= MaxHeaderMinor;
        return new("HeaderVersion", passed, passed ? $"The header names PDF {version.Major}.{version.Minor}." : $"The header names PDF {version.Major}.{version.Minor}; PDF/X-4 is based on PDF 1.6.");
    }

    /// <summary>Checks that the file is not encrypted.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckEncryption(PdfDocument document) =>
        new("Encryption", !document.IsEncrypted, document.IsEncrypted ? "The file is encrypted; PDF/X forbids encryption." : "The file is not encrypted.");

    /// <summary>Checks that the document has a title.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckTitle(PdfDocument document)
    {
        var has = !string.IsNullOrWhiteSpace(PdfDocumentMetadata.GetInfo(document).Title);
        return new("Title", has, has ? "The Info dictionary has a title." : "The Info dictionary has no /Title, which PDF/X-4 requires.");
    }

    /// <summary>Checks the PDF/X output intent: one is present, it names its condition, and it carries a profile unless it names a registry.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The claimed level.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckOutputIntent(PdfDocument document, PdfXLevel level)
    {
        PdfOutputIntent? intent = null;
        foreach (var candidate in PdfDocumentCatalog.GetOutputIntents(document))
        {
            if (candidate.Subtype != PdfXIntent)
            {
                continue;
            }

            intent = candidate;
            break;
        }

        if (intent is null)
        {
            return new(OutputIntentRule, false, "The catalog has no GTS_PDFX output intent.");
        }

        if (string.IsNullOrWhiteSpace(intent.OutputConditionIdentifier))
        {
            return new(OutputIntentRule, false, "The output intent has no /OutputConditionIdentifier.");
        }

        // PDF/X-4p may name an external profile instead of embedding one.
        var needsProfile = intent.Profile is null && level == PdfXLevel.X4 && string.IsNullOrWhiteSpace(intent.RegistryName);
        return needsProfile
            ? new(OutputIntentRule, false, "The output intent embeds no ICC profile and names no registry.")
            : new(OutputIntentRule, true, $"The output intent names {intent.OutputConditionIdentifier}.");
    }

    /// <summary>Checks each page has exactly one of TrimBox or ArtBox.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives one finding for the whole file, or one per failing page.</param>
    private static void CheckPageBoxes(PdfDocument document, List<PdfXFinding> findings)
    {
        var failed = false;
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            var trim = page.Dictionary.ContainsKey(KnownName.TrimBox);
            var art = page.Dictionary.ContainsKey(KnownName.ArtBox);
            if (trim != art)
            {
                continue;
            }

            failed = true;
            findings.Add(new("PageBoxes", false, $"Page {i + 1} has {(trim ? "both a TrimBox and an ArtBox" : "neither a TrimBox nor an ArtBox")}; PDF/X needs exactly one."));
        }

        if (failed)
        {
            return;
        }

        findings.Add(new("PageBoxes", true, "Every page has exactly one of TrimBox or ArtBox."));
    }
}
