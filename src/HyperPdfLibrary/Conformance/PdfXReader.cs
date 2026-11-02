// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks a file's PDF/X claim against what the file holds. PDF/X-4 and PDF/X-4p are checked for the header version,
/// encryption, title, trapping key, output intent, page boxes, annotations, fonts, filters, scripts, embedded and external
/// content, transfer curves and alternate images. Rules that need a full colour and content model are listed as not
/// checked rather than passed. The report is recomputed from the file each time, so a saved edit that breaks a rule shows
/// up as a failed finding next to the unchanged claim.
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

    /// <summary>The start of every PDF/X version name.</summary>
    private const string VersionPrefix = "PDF/X-";

    /// <summary>The overview section that states the document level rules.</summary>
    private const string DocumentReference = "ISO 15930-7; PDF/X in a Nutshell, core principles";

    /// <summary>The requirements the report leaves unchecked.</summary>
    private static readonly string[] NotCheckedRules =
    [
        "Colour spaces and ICC profiles match the output intent.",
        "Annotations that overlap the print area.",
        "Spot colour alternates and consistent spot colour names.",
        "XMP metadata matches the Info dictionary.",
    ];

    /// <summary>Reads the PDF/X claim of a document and checks it. Nothing is changed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfXReport Read(PdfDocument document) => Read(document, CancellationToken.None);

    /// <summary>Reads the PDF/X claim of a document and checks it. Nothing is changed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Stops the check between objects and pages.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfXReport Read(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var xmp = PdfDocumentMetadata.ReadOwnerXmp(document, document.Catalog, cancellationToken);
        var claim = PdfDocumentConformance.ReadPdfXVersion(document, xmp);
        claim = string.IsNullOrWhiteSpace(claim) ? null : claim.Trim();
        var level = Classify(claim);
        if (level is not (PdfXLevel.X4 or PdfXLevel.X4p))
        {
            return new(claim, level, [], [Unchecked(level)]);
        }

        List<PdfXFinding> findings = [CheckHeader(document), CheckEncryption(document), CheckTitle(document), CheckTrapped(document), CheckOutputIntent(document, level)];
        PdfXPageRules.Check(document, findings, cancellationToken);
        PdfXContentRules.Check(document, findings, cancellationToken);
        return new(claim, level, [.. findings], NotCheckedRules);
    }

    /// <summary>Recognises the level a <c>GTS_PDFXVersion</c> value names.</summary>
    /// <param name="claim">The raw value, or <see langword="null"/>.</param>
    /// <returns>The level: <see cref="PdfXLevel.Malformed"/> when the value is not spelled as a PDF/X version name.</returns>
    internal static PdfXLevel Classify(string? claim)
    {
        if (claim is null)
        {
            return PdfXLevel.None;
        }

        if (FromSpelling(claim) is { } exact)
        {
            return exact;
        }

        // A value that only differs from a known name by case or spaces is a misspelt claim, not a conforming one.
        var plain = claim.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (FromSpelling(plain.ToUpperInvariant()) is not null || FromSpelling(plain) is not null)
        {
            return PdfXLevel.Malformed;
        }

        return IsVersionShaped(claim) ? PdfXLevel.Unknown : PdfXLevel.Malformed;
    }

    /// <summary>Maps the exact spelling of a version name to its level.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The level, or <see langword="null"/> when the text is not an exact version name.</returns>
    private static PdfXLevel? FromSpelling(string text) => text switch
    {
        "PDF/X-1:2001" or "PDF/X-1a:2001" or "PDF/X-1a:2003" => PdfXLevel.X1a,
        "PDF/X-2:2003" => PdfXLevel.X2,
        "PDF/X-3:2002" or "PDF/X-3:2003" => PdfXLevel.X3,
        "PDF/X-4" => PdfXLevel.X4,
        "PDF/X-4p" => PdfXLevel.X4p,
        "PDF/X-5g" or "PDF/X-5pg" or "PDF/X-5n" => PdfXLevel.X5,
        "PDF/X-6" or "PDF/X-6n" or "PDF/X-6p" => PdfXLevel.X6,
        _ => null,
    };

    /// <summary>Determines whether text has the shape of a PDF/X version name: the prefix and a part digit.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsVersionShaped(string text) =>
        text.Length > VersionPrefix.Length && text.StartsWith(VersionPrefix, StringComparison.Ordinal) && char.IsAsciiDigit(text[VersionPrefix.Length]);

    /// <summary>Says why a claim was not checked.</summary>
    /// <param name="level">The recognised level.</param>
    /// <returns>The reason, in plain words.</returns>
    private static string Unchecked(PdfXLevel level) => level switch
    {
        PdfXLevel.None => "The file makes no PDF/X claim, so nothing was checked.",
        PdfXLevel.Malformed => "The claim is not spelled as a PDF/X version name, so no level was chosen and nothing was checked.",
        PdfXLevel.Unknown => "The claim names a PDF/X version this library does not know, so nothing was checked.",
        _ => "No rule set exists yet for this PDF/X level, so nothing was checked.",
    };

    /// <summary>Checks that the header version is no newer than PDF 1.6.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckHeader(PdfDocument document)
    {
        var version = PdfDocumentVersioning.GetHeaderVersion(document);
        var passed = version.Major == HeaderMajor && version.Minor <= MaxHeaderMinor;
        var message = passed
            ? $"The header names PDF {version.Major}.{version.Minor}."
            : $"The header names PDF {version.Major}.{version.Minor}; PDF/X-4 is based on PDF 1.6.";
        return new("HeaderVersion", passed, message, DocumentReference);
    }

    /// <summary>Checks that the file is not encrypted.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckEncryption(PdfDocument document) =>
        new("Encryption", !document.IsEncrypted, document.IsEncrypted ? "The file is encrypted; PDF/X forbids encryption." : "The file is not encrypted.", DocumentReference);

    /// <summary>Checks that the document has a title.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckTitle(PdfDocument document)
    {
        var has = !string.IsNullOrWhiteSpace(PdfDocumentMetadata.GetInfo(document).Title);
        return new("Title", has, has ? "The Info dictionary has a title." : "The Info dictionary has no /Title, which PDF/X-4 requires.", DocumentReference);
    }

    /// <summary>Checks that the document says whether it is trapped, as <c>True</c> or <c>False</c>.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckTrapped(PdfDocument document)
    {
        var trapped = PdfDocumentMetadata.GetInfo(document).Trapped;
        var passed = trapped is "True" or "False";
        return new("Trapped", passed, passed ? $"The Info dictionary says Trapped is {trapped}." : "The Info dictionary has no /Trapped of True or False, which PDF/X-4 requires.", DocumentReference);
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
            return new(OutputIntentRule, false, "The catalog has no GTS_PDFX output intent.", DocumentReference);
        }

        if (string.IsNullOrWhiteSpace(intent.OutputConditionIdentifier))
        {
            return new(OutputIntentRule, false, "The output intent has no /OutputConditionIdentifier.", DocumentReference);
        }

        // PDF/X-4p may name an external profile instead of embedding one.
        var needsProfile = intent.Profile is null && level == PdfXLevel.X4 && string.IsNullOrWhiteSpace(intent.RegistryName);
        return needsProfile
            ? new(OutputIntentRule, false, "The output intent embeds no ICC profile and names no registry.", DocumentReference)
            : new(OutputIntentRule, true, $"The output intent names {intent.OutputConditionIdentifier}.", DocumentReference);
    }
}
