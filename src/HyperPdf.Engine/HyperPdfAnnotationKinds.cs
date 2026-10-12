// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationKinds annotation operations.</summary>
internal static class HyperPdfAnnotationKinds
{
    /// <summary>The colour PDF readers give a highlight without one.</summary>
    internal const uint DefaultHighlight = 0xFFFF00;

    /// <summary>The colour PDF readers give other annotations without one.</summary>
    internal const uint DefaultColor = 0;

    /// <summary>Gets SignatureSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetSignatureSubject() => "Signature"u8;

    /// <summary>Gets TextBoxSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetTextBoxSubject() => "Text box"u8;

    /// <summary>Gets ArrowSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetArrowSubject() => "Arrow"u8;

    /// <summary>Gets LineSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetLineSubject() => "Line"u8;

    /// <summary>Gets StampSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetStampSubject() => "Stamp"u8;

    /// <summary>Gets CalloutSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetCalloutSubject() => "Callout"u8;

    /// <summary>Gets PolygonSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetPolygonSubject() => "Polygon"u8;

    /// <summary>Gets CloudSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetCloudSubject() => "Cloud"u8;

    /// <summary>Gets PolyLineSubject.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetPolyLineSubject() => "Polyline"u8;

    /// <summary>Maps the subtypes that need no subject to a kind.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns>The kind.</returns>
    internal static AnnotationKind SimpleKind(KnownName subtype) => subtype switch
    {
        KnownName.Text => AnnotationKind.Note,
        KnownName.Highlight => AnnotationKind.Highlight,
        KnownName.Underline => AnnotationKind.Underline,
        KnownName.Squiggly => AnnotationKind.Squiggly,
        KnownName.StrikeOut => AnnotationKind.StrikeOut,
        KnownName.PolyLine => AnnotationKind.PolyLine,
        KnownName.Square => AnnotationKind.Rectangle,
        KnownName.Circle => AnnotationKind.Ellipse,
        KnownName.Redact => AnnotationKind.Redaction,
        _ => AnnotationKind.Other,
    };

    /// <summary>Gets an annotation's kind, or <see langword="null"/> for links, form fields and pop-ups, which are not listed.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The kind.</returns>
    internal static AnnotationKind? GetKind(HyperPdfAnnotations annotationState, PdfDictionary annotation)
    {
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        return subtype switch
        {
            KnownName.Link or KnownName.Popup or KnownName.Widget => null,
            KnownName.FreeText => HasSubject(annotationState, annotation, GetCalloutSubject()) ? AnnotationKind.Callout : AnnotationKind.TextBox,
            KnownName.Polygon => GetSavedPolygonKind(annotationState, annotation),
            KnownName.Ink => GetInkKind(annotationState, annotation),
            KnownName.Stamp => GetStampKind(annotationState, annotation),
            _ => SimpleKind(subtype),
        };
    }

    /// <summary>Tells signatures, stamps, callouts and text boxes written here apart from other stamps.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The stamp.</param>
    /// <returns>The kind.</returns>
    internal static AnnotationKind GetStampKind(HyperPdfAnnotations annotationState, PdfDictionary annotation)
    {
        if (HasSubject(annotationState, annotation, GetSignatureSubject()))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotationState, annotation, GetStampSubject()))
        {
            return AnnotationKind.Stamp;
        }

        if (HasSubject(annotationState, annotation, GetCalloutSubject()))
        {
            return AnnotationKind.Callout;
        }

        return HasSubject(annotationState, annotation, GetTextBoxSubject()) ? AnnotationKind.TextBox : AnnotationKind.Other;
    }

    /// <summary>Tells signatures, arrows, lines and polygons drawn as ink apart from freehand ink.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind.</returns>
    internal static AnnotationKind GetInkKind(HyperPdfAnnotations annotationState, PdfDictionary annotation)
    {
        if (HasSubject(annotationState, annotation, GetSignatureSubject()))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotationState, annotation, GetArrowSubject()))
        {
            return AnnotationKind.Arrow;
        }

        return HasSubject(annotationState, annotation, GetLineSubject()) ? AnnotationKind.Line : GetPolygonKind(annotationState, annotation) ?? AnnotationKind.Ink;
    }

    /// <summary>Tells polygons, clouds and runs of lines drawn as ink apart from freehand ink.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind, or <see langword="null"/> for plain ink.</returns>
    internal static AnnotationKind? GetPolygonKind(HyperPdfAnnotations annotationState, PdfDictionary annotation)
    {
        if (HasSubject(annotationState, annotation, GetPolygonSubject()))
        {
            return AnnotationKind.Polygon;
        }

        if (HasSubject(annotationState, annotation, GetCloudSubject()))
        {
            return AnnotationKind.Cloud;
        }

        return HasSubject(annotationState, annotation, GetPolyLineSubject()) ? AnnotationKind.PolyLine : null;
    }

    /// <summary>Tells a cloud from a plain polygon: by the subject written here, or the cloud intent.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The polygon.</param>
    /// <returns>The kind.</returns>
    internal static AnnotationKind GetSavedPolygonKind(HyperPdfAnnotations annotationState, PdfDictionary annotation) =>
        HasSubject(annotationState, annotation, GetCloudSubject()) || annotation.GetName(KnownName.IT) == annotationState.Names.PolygonCloud ? AnnotationKind.Cloud : AnnotationKind.Polygon;

    /// <summary>Determines whether an annotation's <c>/Subj</c> equals a value.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subject">The subject.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasSubject(HyperPdfAnnotations annotationState, PdfDictionary annotation, ReadOnlySpan<byte> subject) => PdfAnnotations.TextEquals(
        annotation,
        annotationState.Names.Subject,
        subject);

    /// <summary>
    /// Gets the colour an annotation shows: its <c>/C</c>; else the first colour its appearance draws with; else the
    /// colour PDF readers default to.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    internal static uint GetColor(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind)
    {
        if (PdfAnnotations.TryGetColor(annotation, KnownName.C, out var color))
        {
            return color;
        }

        if (PdfAnnotations.GetNormalAppearance(annotation) is not { } appearance)
        {
            return annotation.IsName(KnownName.Subtype, KnownName.Highlight) ? DefaultHighlight : DefaultColor;
        }

        if (PdfAppearanceColors.TryReadFirstColor(appearance, annotationState.Store.Names, out color))
        {
            return color;
        }

        return kind is AnnotationKind.Signature or AnnotationKind.TextBox ? AnnotationColors.Ink : AnnotationColors.Sand;
    }

    /// <summary>Determines whether an annotation is removed but kept.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsRemoved(HyperPdfAnnotations annotationState, PdfDictionary annotation) => PdfAnnotations.HasText(annotation, annotationState.Names.Removed);

    /// <summary>Determines whether an annotation is a reply, so it is listed with its comment rather than on its own.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a reply.</returns>
    internal static bool IsReply(HyperPdfAnnotations annotationState, PdfDictionary annotation) =>
        PdfAnnotations.HasText(annotation, annotationState.Names.PendingReply) || PdfAnnotations.GetInReplyTo(annotation) is not null;

    /// <summary>Determines whether an annotation may be moved or restyled: listed, not removed, not a reply and not text markup.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns><see langword="true"/> when it may be changed.</returns>
    internal static bool IsEditable(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind) =>
        kind is not (AnnotationKind.Highlight or AnnotationKind.Underline or AnnotationKind.StrikeOut or AnnotationKind.Squiggly)
        && !IsRemoved(annotationState, annotation)
        && !IsReply(annotationState, annotation);
}
