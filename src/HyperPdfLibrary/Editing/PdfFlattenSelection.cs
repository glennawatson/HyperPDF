// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Decides which annotations a flatten works on and which of them are drawn, from their type and their flags
/// (ISO 32000-2 12.5.2, 12.5.3).
/// </summary>
internal static class PdfFlattenSelection
{
    /// <summary>Decides what flattening does with an annotation, before its appearance is looked at.</summary>
    /// <param name="options">What to flatten.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The decision.</returns>
    internal static PdfFlattenDecision Decide(PdfFlattenOptions options, PdfDictionary annotation)
    {
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        if (subtype == KnownName.Popup || !InScope(options.Targets, subtype))
        {
            return PdfFlattenDecision.OutOfScope;
        }

        if (GetKeptReason(subtype) is { } kept)
        {
            return PdfFlattenDecision.Keep(kept);
        }

        if (GetHiddenReason(options.Mode, (PdfAnnotationFlags)annotation.GetInt32(KnownName.F)) is not { } reason)
        {
            return PdfFlattenDecision.Draw;
        }

        // A form field that is not shown leaves the document with its form, so its widget goes too.
        return subtype == KnownName.Widget ? PdfFlattenDecision.Remove(reason) : PdfFlattenDecision.Keep(reason);
    }

    /// <summary>Works out whether a kind of annotation is never drawn into the page.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns>The reason, or <see langword="null"/> when the kind can be drawn.</returns>
    private static PdfFlattenSkipReason? GetKeptReason(KnownName subtype) => subtype switch
    {
        KnownName.TrapNet or KnownName.PrinterMark => PdfFlattenSkipReason.ProductionMark,
        KnownName.Redact => PdfFlattenSkipReason.RedactionNotApplied,
        _ => null,
    };

    /// <summary>Determines whether a kind of annotation is one flattening was asked to work on.</summary>
    /// <param name="targets">Which kinds of annotation.</param>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool InScope(PdfFlattenTargets targets, KnownName subtype) => targets switch
    {
        PdfFlattenTargets.WidgetsOnly => subtype == KnownName.Widget,
        PdfFlattenTargets.MarkupOnly => IsMarkup(subtype),
        PdfFlattenTargets.WithoutLinks => subtype != KnownName.Link,
        _ => true,
    };

    /// <summary>Determines whether a subtype is a markup annotation (ISO 32000-2 12.5.6.2).</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <returns><see langword="true"/> for a markup annotation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMarkup(KnownName subtype) =>
        IsTextMarkup(subtype) || IsGeometricMarkup(subtype) || subtype is KnownName.FileAttachment or KnownName.Sound or KnownName.Redact;

    /// <summary>Recognises notes and text-markup subtypes.</summary>
    /// <param name="subtype">The parsed subtype.</param>
    /// <returns>Whether it belongs to this markup group.</returns>
    private static bool IsTextMarkup(KnownName subtype) =>
        subtype is KnownName.Text or KnownName.FreeText or KnownName.Highlight or KnownName.Underline
            or KnownName.Squiggly or KnownName.StrikeOut or KnownName.Caret;

    /// <summary>Recognises line, shape, ink and stamp markup.</summary>
    /// <param name="subtype">The parsed subtype.</param>
    /// <returns>Whether it belongs to this markup group.</returns>
    private static bool IsGeometricMarkup(KnownName subtype) =>
        subtype is KnownName.Line or KnownName.Square or KnownName.Circle or KnownName.Polygon
            or KnownName.PolyLine or KnownName.Ink or KnownName.Stamp;

    /// <summary>Works out whether an annotation's flags keep it from being drawn.</summary>
    /// <param name="mode">Whether the page is drawn for the screen or for printing.</param>
    /// <param name="flags">The annotation's flags.</param>
    /// <returns>The reason, or <see langword="null"/> when the annotation is drawn.</returns>
    private static PdfFlattenSkipReason? GetHiddenReason(PdfFlattenMode mode, PdfAnnotationFlags flags)
    {
        if ((flags & PdfAnnotationFlags.Hidden) != 0)
        {
            return PdfFlattenSkipReason.Hidden;
        }

        if (mode == PdfFlattenMode.Print)
        {
            return (flags & PdfAnnotationFlags.Print) == 0 ? PdfFlattenSkipReason.NotPrinted : null;
        }

        return (flags & PdfAnnotationFlags.NoView) != 0 ? PdfFlattenSkipReason.NotShown : null;
    }
}
