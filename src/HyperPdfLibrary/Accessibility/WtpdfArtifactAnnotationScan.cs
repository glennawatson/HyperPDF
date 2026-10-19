// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks that annotations which must be artifacts are not tagged as content (WTPDF 1.0, clauses 8.9.2.2, 8.9.2.4.13 and
/// 8.9.2.4.14): an annotation with the Invisible flag, or with NoView but not ToggleNoView, a widget with no width and height,
/// and a printer's mark.
/// </summary>
internal static class WtpdfArtifactAnnotationScan
{
    /// <summary>The Invisible annotation flag (ISO 32000-2 Table 167, bit 1).</summary>
    private const int Invisible = 1;

    /// <summary>The position of the right edge in an annotation rectangle.</summary>
    private const int RightIndex = 2;

    /// <summary>The position of the top edge in an annotation rectangle.</summary>
    private const int TopIndex = 3;

    /// <summary>Edges closer than this, in points, count as the same edge.</summary>
    private const double SizeTolerance = 0.001;

    /// <summary>The NoView annotation flag (bit 6).</summary>
    private const int NoView = 1 << 5;

    /// <summary>The ToggleNoView annotation flag (bit 9).</summary>
    private const int ToggleNoView = 1 << 8;

    /// <summary>The number of numbers in an annotation rectangle.</summary>
    private const int RectLength = 4;

    /// <summary>Reports each such annotation that sits in the structure tree outside an Artifact tag.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tree">The structure tree, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfDocument document, PdfStructureTree? tree, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        if (tree is null)
        {
            return;
        }

        var tagged = TaggedObjects(tree, cancellationToken);
        for (var index = 0; tagged.Count > 0 && index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPage(PdfDocumentPages.GetPage(document, index).Dictionary, tagged, findings);
        }
    }

    /// <summary>Collects the object numbers the tree references outside Artifact tags.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The object numbers.</returns>
    private static HashSet<int> TaggedObjects(PdfStructureTree tree, CancellationToken cancellationToken)
    {
        var numbers = new HashSet<int>();
        StructureWalk.Each(tree, numbers, AddObjects, cancellationToken);
        return numbers;
    }

    /// <summary>Adds the objects an element references, unless the element is an Artifact.</summary>
    /// <param name="element">The element.</param>
    /// <param name="numbers">Receives the object numbers.</param>
    private static void AddObjects(PdfStructureElement element, HashSet<int> numbers)
    {
        if (element.MappedType == "Artifact")
        {
            return;
        }

        foreach (var kid in element.KidSpan)
        {
            if (kid.Element is null && kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                _ = numbers.Add(kid.Object.Number);
            }
        }
    }

    /// <summary>Checks one page's annotations.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="tagged">The object numbers tagged as content.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckPage(PdfDictionary page, HashSet<int> tagged, List<PdfWtpdfFinding> findings)
    {
        if (page.GetArray(KnownName.Annots) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            var raw = annotations.GetRaw(i);
            if (raw.IsReference && tagged.Contains(raw.AsReference().Number) && annotations.GetDictionary(i) is { } annotation && MustBeArtifact(annotation))
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.2", "An annotation that is hidden, has no size or is a printer's mark must be an artifact, not tagged as content.", 0));
            }
        }
    }

    /// <summary>Determines whether an annotation must be an artifact.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it must.</returns>
    private static bool MustBeArtifact(PdfDictionary annotation)
    {
        var flags = annotation.GetInt32(KnownName.F, 0);
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        return (flags & Invisible) != 0
            || ((flags & NoView) != 0 && (flags & ToggleNoView) == 0)
            || subtype == KnownName.PrinterMark
            || (subtype == KnownName.Widget && HasNoSize(annotation));
    }

    /// <summary>Determines whether an annotation's rectangle has no width and no height.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it has none.</returns>
    private static bool HasNoSize(PdfDictionary annotation) =>
        annotation.GetArray(KnownName.Rect) is { Count: RectLength } rect
        && Math.Abs(rect.GetNumber(RightIndex) - rect.GetNumber(0)) < SizeTolerance
        && Math.Abs(rect.GetNumber(TopIndex) - rect.GetNumber(1)) < SizeTolerance;
}
