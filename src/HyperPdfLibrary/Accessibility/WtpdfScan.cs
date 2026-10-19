// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks a file against Well-Tagged PDF 1.0 (PDF Association, clause 8.2) and reads which levels it declares.</summary>
internal static class WtpdfScan
{
    /// <summary>The reuse declaration's profile address (WTPDF 1.0, clause 6).</summary>
    internal const string ReuseUri = "http://pdfa.org/declarations/wtpdf#reuse1.0";

    /// <summary>The reuse address as the WTPDF 1.0 text spells it, with a slash before the fragment. veraPDF 1.30 rejects this spelling, so it is read but never written.</summary>
    internal const string ReuseUriWithSlash = "http://pdfa.org/declarations/wtpdf/#reuse1.0";

    /// <summary>The accessibility address as the WTPDF 1.0 text spells it. It is read but never written.</summary>
    internal const string AccessibilityUriWithSlash = "http://pdfa.org/declarations/wtpdf/#accessibility1.0";

    /// <summary>The accessibility declaration's profile address (WTPDF 1.0, clause 6).</summary>
    internal const string AccessibilityUri = "http://pdfa.org/declarations/wtpdf#accessibility1.0";

    /// <summary>Checks a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The report.</returns>
    internal static PdfWtpdfReport Check(PdfDocument document, CancellationToken cancellationToken)
    {
        var findings = new List<PdfWtpdfFinding>();
        var xmp = PdfDocumentMetadata.GetXmp(document);
        var declarations = xmp?.Declarations ?? [];
        CheckStructure(document, findings, cancellationToken);
        WtpdfAnnotationScan.Run(document, findings, cancellationToken);
        var tree = PdfDocumentTagged.GetStructureTree(document);
        WtpdfArtifactAnnotationScan.Run(document, tree, findings, cancellationToken);
        WtpdfAnnotationTreeScan.Run(document.Objects, tree, findings, cancellationToken);
        WtpdfEmbeddedFileScan.Run(document, findings, cancellationToken);
        WtpdfNavigationScan.Run(document, findings, cancellationToken);
        WtpdfFontScan.Run(document, findings, cancellationToken);
        WtpdfSignatureScan.Run(document, findings, cancellationToken);
        CheckPdfUa(document, findings, cancellationToken);
        var report = PdfAccessibilityReader.Read(document, cancellationToken);
        WtpdfReportScan.Check(report, findings);
        CheckFile(document, xmp is not null, report.DisplayDocTitle, findings);
        return new(
            Holds(declarations, ReuseUri) || Holds(declarations, ReuseUriWithSlash),
            Holds(declarations, AccessibilityUri) || Holds(declarations, AccessibilityUriWithSlash),
            [.. findings]);
    }

    /// <summary>Requires PDF 2.0 and an XMP metadata stream, as veraPDF's WTPDF profile checks (clauses 6.1 and 8.11.1).</summary>
    /// <param name="document">The document.</param>
    /// <param name="hasMetadata">Whether the catalog has an XMP metadata stream.</param>
    /// <param name="displayDocTitle">Whether the viewer preferences set DisplayDocTitle to true.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckFile(PdfDocument document, bool hasMetadata, bool displayDocTitle, List<PdfWtpdfFinding> findings)
    {
        if (PdfDocumentVersioning.GetVersion(document) < PdfVersion.Pdf20)
        {
            Add(findings, PdfWtpdfProfile.Reuse, "6.1", "The file must be PDF 2.0, in its header or in the catalog's Version entry.", null);
        }

        if (!hasMetadata)
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.11.1", "The catalog needs a Metadata entry that is an XMP metadata stream.", null);
        }

        if (!displayDocTitle)
        {
            Add(findings, PdfWtpdfProfile.Accessibility, "8.11.2", "The viewer preferences must set DisplayDocTitle to true, so a reader shows the title rather than the file name.", null);
        }
    }

    /// <summary>Determines whether a declaration names a profile.</summary>
    /// <param name="declarations">The file's declarations.</param>
    /// <param name="uri">The profile address.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool Holds(PdfDeclaration[] declarations, string uri)
    {
        foreach (var declaration in declarations)
        {
            if (string.Equals(declaration.ConformsTo, uri, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Adds a finding.</summary>
    /// <param name="findings">Receives it.</param>
    /// <param name="profile">The lowest level it applies to.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="message">The message.</param>
    /// <param name="element">The tag involved, or <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(List<PdfWtpdfFinding> findings, PdfWtpdfProfile profile, string clause, string message, PdfStructureElement? element) =>
        findings.Add(new(profile, clause, message, element?.Id.Number ?? 0));

    /// <summary>Requires PDF/UA-2 conformance, which the accessibility level adds (WTPDF 1.0, clause 6 and Appendix C).</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the report.</param>
    private static void CheckPdfUa(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        var report = PdfAccessibilityReader.Read(document, cancellationToken);
        if (report.Claim.Part != PdfUaClaim.Part2)
        {
            Add(findings, PdfWtpdfProfile.Accessibility, "6", "The accessibility level needs the file to claim PDF/UA-2 (pdfuaid:part 2).", null);
            return;
        }

        if (report.Findings.Count > 0)
        {
            Add(findings, PdfWtpdfProfile.Accessibility, "6", $"The accessibility level needs PDF/UA-2 to hold, and the accessibility report lists {report.Findings.Count} problems.", null);
        }
    }

    /// <summary>Walks the structure tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    private static void CheckStructure(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.2", "The file has no structure tree, so its content is not enclosed in tags.", null);
            return;
        }

        CheckRoot(tree, findings);
        StructureWalk.Each(tree, findings, CheckElement, cancellationToken);
    }

    /// <summary>Requires one Document in the PDF 2.0 namespace as the only child of the root (clause 8.2.5.2).</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckRoot(PdfStructureTree tree, List<PdfWtpdfFinding> findings)
    {
        if (tree.Roots.Count != 1 || !string.Equals(tree.Roots[0].MappedType, "Document", StringComparison.Ordinal))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.2", "The structure tree root must hold a single Document tag as its only child.", null);
            return;
        }

        if (!string.Equals(tree.Roots[0].Namespace, PdfStructureTypes.Pdf20Namespace, StringComparison.Ordinal))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.2", "The Document tag must be in the PDF 2.0 namespace.", tree.Roots[0]);
        }
    }

    /// <summary>Checks one tag by its type.</summary>
    /// <param name="element">The tag.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckElement(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        WtpdfTextScan.Check(element, findings);
        switch (element.MappedType)
        {
            case "H":
            {
                Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.12", "Use a numbered heading (H1 to H6), not H.", element);
                break;
            }

            case "Note":
            {
                Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.14.1", "Use FENote, not Note.", element);
                break;
            }

            case "Figure":
            {
                CheckFigure(element, findings);
                break;
            }

            case "Formula":
            {
                CheckFormula(element, findings);
                break;
            }

            case "L":
            {
                CheckList(element, findings);
                WtpdfReferenceScan.CheckContinuedList(element, findings);
                break;
            }

            default:
            {
                CheckGrouped(element, findings);
                break;
            }
        }
    }

    /// <summary>Checks the tags that hold a fixed shape of other tags.</summary>
    /// <param name="element">The tag.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckGrouped(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        switch (element.MappedType)
        {
            case "Form" when element.Dictionary.Owner is { } store:
            {
                WtpdfFormScan.Check(element, store, findings);
                break;
            }

            case "FENote":
            {
                WtpdfReferenceScan.CheckNote(element, findings);
                break;
            }

            case "TOCI":
            {
                WtpdfReferenceScan.CheckTocItem(element, findings);
                break;
            }

            case "Table":
            {
                WtpdfTableScan.Check(element, findings);
                break;
            }

            case "Ruby":
            {
                CheckRuby(element, findings);
                break;
            }

            case "Warichu":
            {
                CheckWarichu(element, findings);
                break;
            }

            case "Caption":
            {
                CheckCaption(element, findings);
                break;
            }

            default:
            {
                CheckMath(element, findings);
                break;
            }
        }
    }

    /// <summary>Requires an alternate description or replacement text on a Figure at the accessibility level (clause 8.2.5.28.2).</summary>
    /// <param name="element">The Figure.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckFigure(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        if (string.IsNullOrEmpty(element.AlternateText) && string.IsNullOrEmpty(element.ActualText))
        {
            Add(findings, PdfWtpdfProfile.Accessibility, "8.2.5.28.2", "A Figure needs an alternate description (Alt) or replacement text (ActualText).", element);
        }
    }

    /// <summary>Requires a formula without MathML to have <c>Alt</c> or <c>ActualText</c> at the accessibility level (clause 8.2.5.29.2).</summary>
    /// <param name="element">The Formula.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckFormula(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        if (string.IsNullOrEmpty(element.AlternateText) && string.IsNullOrEmpty(element.ActualText) && !HasMathKid(element))
        {
            Add(findings, PdfWtpdfProfile.Accessibility, "8.2.5.29.2", "A Formula needs an Alt or ActualText entry when it has no MathML.", element);
        }
    }

    /// <summary>Determines whether a tag has a MathML <c>math</c> child.</summary>
    /// <param name="element">The tag.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasMathKid(PdfStructureElement element)
    {
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is { } child && string.Equals(child.RawType, "math", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Requires a ListNumbering other than None when a list has labels (clause 8.2.5.25).</summary>
    /// <param name="list">The L tag.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckList(PdfStructureElement list, List<PdfWtpdfFinding> findings)
    {
        if (HasLabel(list) && (NearestListNumbering(list) is not { Length: > 0 } name || string.Equals(name, "None", StringComparison.Ordinal)))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.25", "A list with labels (Lbl) needs a ListNumbering value other than None.", list);
        }
    }

    /// <summary>Finds the ListNumbering a list inherits.</summary>
    /// <param name="list">The L tag.</param>
    /// <returns>The nearest value on the list or an ancestor, or <see langword="null"/> when none is given.</returns>
    private static string? NearestListNumbering(PdfStructureElement list)
    {
        for (PdfStructureElement? current = list; current is not null; current = current.Parent)
        {
            if (current.Attributes.ListNumbering is { Length: > 0 } name)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Determines whether any item of a list has a label.</summary>
    /// <param name="list">The L tag.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasLabel(PdfStructureElement list)
    {
        foreach (var item in list.KidSpan)
        {
            if (item.Element is not { } child)
            {
                continue;
            }

            foreach (var part in child.KidSpan)
            {
                if (part.Element is { } label && string.Equals(label.MappedType, "Lbl", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Requires a Warichu to hold WP, WT and WP in that order (clause 8.2.5.24).</summary>
    /// <param name="element">The Warichu tag.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckWarichu(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        var names = KidNames(element);
        if (!names.SequenceEqual(["WP", "WT", "WP"]))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.24", "A Warichu must hold a WP, a WT and a WP, in that order.", element);
        }
    }

    /// <summary>Requires a Ruby to hold RB and RT, or RB, RP, RT and RP (clause 8.2.5.23).</summary>
    /// <param name="element">The Ruby tag.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckRuby(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        var names = KidNames(element);
        if (!names.SequenceEqual(["RB", "RT"]) && !names.SequenceEqual(["RB", "RP", "RT", "RP"]))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.23", "A Ruby must hold an RB and an RT, or an RB, RP, RT and RP, in that order.", element);
        }
    }

    /// <summary>Requires a caption to be the first or last tag in its parent (clause 8.2.5.27).</summary>
    /// <param name="element">The Caption tag.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckCaption(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        if (element.Parent is not { } parent)
        {
            return;
        }

        var names = KidNames(parent);
        if (names.Count > 1 && !string.Equals(names[0], "Caption", StringComparison.Ordinal) && !string.Equals(names[^1], "Caption", StringComparison.Ordinal))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.27", "A Caption must be the first or the last tag inside the tag it describes.", element);
        }
    }

    /// <summary>Requires a MathML math tag to sit directly inside a Formula (clause 8.2.5.29.1).</summary>
    /// <param name="element">The tag.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckMath(PdfStructureElement element, List<PdfWtpdfFinding> findings)
    {
        if (string.Equals(element.RawType, "math", StringComparison.Ordinal) && !string.Equals(element.Parent?.MappedType, "Formula", StringComparison.Ordinal))
        {
            Add(findings, PdfWtpdfProfile.Reuse, "8.2.5.29.1", "A math tag must be a child of a Formula tag.", element);
        }
    }

    /// <summary>Lists the types of an element's child tags.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The types, in order.</returns>
    private static List<string> KidNames(PdfStructureElement element)
    {
        var names = new List<string>(element.Kids.Count);
        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is { } child)
            {
                names.Add(child.MappedType);
            }
        }

        return names;
    }
}
