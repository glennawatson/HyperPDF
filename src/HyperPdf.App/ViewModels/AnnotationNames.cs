// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;

namespace HyperPdf.App.ViewModels;

/// <summary>Names of annotation kinds and colours shown to the user.</summary>
public static class AnnotationNames
{
    /// <summary>Gets the name of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string Get(AnnotationKind kind) => kind switch
    {
        AnnotationKind.Highlight => "Highlight",
        AnnotationKind.Underline => "Underline",
        AnnotationKind.StrikeOut => "Strike-out",
        AnnotationKind.Squiggly => "Squiggly underline",
        AnnotationKind.Ink => "Drawing",
        AnnotationKind.Note => "Note",
        AnnotationKind.TextBox => "Text",
        AnnotationKind.Signature => "Signature",
        AnnotationKind.Rectangle => "Rectangle",
        AnnotationKind.Ellipse => "Ellipse",
        AnnotationKind.Arrow => "Arrow",
        AnnotationKind.Line => "Line",
        AnnotationKind.Stamp => "Stamp",
        AnnotationKind.Callout => "Callout",
        AnnotationKind.Polygon => "Polygon",
        AnnotationKind.Cloud => "Cloud",
        AnnotationKind.PolyLine => "Connected lines",
        AnnotationKind.Redaction => "Redaction",
        _ => "Annotation",
    };

    /// <summary>Gets the name of an annotation colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The name, or "Custom".</returns>
    public static string GetColor(uint color) => AnnotationColors.GetName(color) ?? "Custom";
}
