// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Names of annotation kinds and colours shown to the user.</summary>
public static class AnnotationNames
{
    /// <summary>Gets the name of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string Get(AnnotationKind kind) => kind switch
    {
        AnnotationKind.Highlight => UiText.Get("Highlight"),
        AnnotationKind.Underline => UiText.Get("Underline"),
        AnnotationKind.StrikeOut => UiText.Get("Strike-out"),
        AnnotationKind.Squiggly => UiText.Get("Squiggly underline"),
        AnnotationKind.Ink => UiText.Get("Drawing"),
        AnnotationKind.Note => UiText.Get("Note"),
        AnnotationKind.TextBox => UiText.Get("Text"),
        AnnotationKind.Signature => UiText.Get("Signature"),
        AnnotationKind.Rectangle => UiText.Get("Rectangle"),
        AnnotationKind.Ellipse => UiText.Get("Ellipse"),
        AnnotationKind.Arrow => UiText.Get("Arrow"),
        AnnotationKind.Line => UiText.Get("Line"),
        AnnotationKind.Stamp => UiText.Get("Stamp"),
        AnnotationKind.Callout => UiText.Get("Callout"),
        AnnotationKind.Polygon => UiText.Get("Polygon"),
        AnnotationKind.Cloud => UiText.Get("Cloud"),
        AnnotationKind.PolyLine => UiText.Get("Connected lines"),
        AnnotationKind.Redaction => UiText.Get("Redaction"),
        AnnotationKind.Caret => UiText.Get("Insertion point"),
        AnnotationKind.FreehandHighlight => UiText.Get("Freehand highlight"),
        _ => UiText.Get("Annotation"),
    };

    /// <summary>Gets the name of an annotation colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The name, or "Custom".</returns>
    public static string GetColor(uint color) => AnnotationColors.GetName(color) ?? UiText.Get("Custom");
}
