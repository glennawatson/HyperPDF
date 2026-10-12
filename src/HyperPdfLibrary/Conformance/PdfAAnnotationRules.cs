// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>PDF/A rules on annotation types, flags, opacity and colours (ISO 19005-1 6.5, ISO 19005-2/3/4 6.3).</summary>
internal static class PdfAAnnotationRules
{
    /// <summary>The Invisible annotation flag.</summary>
    private const long InvisibleFlag = 1;

    /// <summary>The Hidden annotation flag.</summary>
    private const long HiddenFlag = 2;

    /// <summary>The Print annotation flag.</summary>
    private const long PrintFlag = 4;

    /// <summary>The NoView annotation flag.</summary>
    private const long NoViewFlag = 32;

    /// <summary>The ToggleNoView annotation flag.</summary>
    private const long ToggleNoViewFlag = 256;

    /// <summary>The number of values in a rectangle.</summary>
    private const int RectangleValues = 4;

    /// <summary>The index of a rectangle's second x coordinate.</summary>
    private const int Right = 2;

    /// <summary>The index of a rectangle's second y coordinate.</summary>
    private const int Top = 3;

    /// <summary>The flags PDF/A-1 forbids: Invisible, Hidden and NoView.</summary>
    private const long ForbiddenFlagsPdfA1 = InvisibleFlag | HiddenFlag | NoViewFlag;

    /// <summary>The flags PDF/A-2 and later forbid: Invisible, Hidden, NoView and ToggleNoView.</summary>
    private const long ForbiddenFlags = ForbiddenFlagsPdfA1 | ToggleNoViewFlag;

    /// <summary>The annotation types PDF/A-1 allows.</summary>
    private static readonly string[] TypesPdfA1 =
    [
        "Text", "Link", "FreeText", "Line", "Square", "Circle", "Highlight", "Underline", "Squiggly", "StrikeOut", "Stamp", "Ink",
        "Popup", "Widget", "PrinterMark", "TrapNet",
    ];

    /// <summary>The annotation types PDF/A-4 allows, without the ones its conformance levels add.</summary>
    private static readonly string[] TypesPdfA4 =
    [
        "Text", "Link", "FreeText", "Line", "Square", "Circle", "Polygon", "PolyLine", "Highlight", "Underline", "Squiggly",
        "StrikeOut", "Stamp", "Caret", "Ink", "Popup", "Widget", "PrinterMark", "TrapNet", "Watermark", "Redact", "Projection",
    ];

    /// <summary>The annotation types PDF/A-2 and PDF/A-3 allow.</summary>
    private static readonly string[] TypesPdfA2 =
    [
        "Text", "Link", "FreeText", "Line", "Square", "Circle", "Polygon", "PolyLine", "Highlight", "Underline", "Squiggly",
        "StrikeOut", "Stamp", "Caret", "Ink", "Popup", "FileAttachment", "Widget", "PrinterMark", "TrapNet", "Watermark", "Redact",
    ];

    /// <summary>The annotation types PDF/A-4e adds to <see cref="TypesPdfA4"/>.</summary>
    private static readonly string[] TypesPdfA4E = ["3D", "RichMedia"];

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.AnnotationTypePdfA1,
        PdfARule.AnnotationTypePdfA2,
        PdfARule.AnnotationTypePdfA4,
        PdfARule.AnnotationTypePdfA4E,
        PdfARule.AnnotationTypePdfA4F,
        PdfARule.AnnotationFlagsPdfA1,
        PdfARule.AnnotationFlags,
        PdfARule.AnnotationFlagsPresent,
        PdfARule.AnnotationFlagsPresentPdfA4,
        PdfARule.AnnotationOpacity,
        PdfARule.AnnotationColorNeedsRgbIntent,
    ];

    /// <summary>Gets the ICC colour space of an RGB profile.</summary>
    private static ReadOnlySpan<byte> RgbSpace => "RGB "u8;

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var annotations = PdfAAnnotations.Read(context);
        if (annotations.Count == 0)
        {
            return;
        }

        var names = context.Document.Objects.Names;
        var types = new PdfAAnnotationTypes(
            PdfAAnnotations.Intern(names, TypesPdfA1),
            PdfAAnnotations.Intern(names, TypesPdfA2),
            PdfAAnnotations.Intern(names, TypesPdfA4),
            PdfAAnnotations.Intern(names, TypesPdfA4E),
            names.Intern("FileAttachment"u8));
        var rgbIntent = PdfAOutputIntentColorSpace.Read(context.Document.Catalog) is { } space && RgbSpace.SequenceEqual(space);
        foreach (var annotation in annotations)
        {
            CheckType(context, types, annotation);
            CheckFlags(context, annotation);
            var dictionary = annotation.Dictionary;
            var opacity = dictionary.Get(KnownName.CA);
            PdfAAnnotations.Require(context, !opacity.IsNumber || opacity.AsNumber() is 1.0, PdfARule.AnnotationOpacity, annotation);
            var coloured = dictionary.ContainsKey(KnownName.C) || dictionary.ContainsKey(KnownName.IC);
            PdfAAnnotations.Require(context, !coloured || rgbIntent, PdfARule.AnnotationColorNeedsRgbIntent, annotation);
        }
    }

    /// <summary>Determines whether an annotation's <c>/Rect</c> has no width and no height.</summary>
    /// <param name="annotation">The annotation dictionary.</param>
    /// <returns><see langword="true"/> when it has neither.</returns>
    internal static bool IsPoint(PdfDictionary annotation) =>
        annotation.GetArray(KnownName.Rect) is { Count: >= RectangleValues } rect
        && rect.Get(0).IsNumber && rect.Get(1).IsNumber && rect.Get(Right).IsNumber && rect.Get(Top).IsNumber
        && rect.GetNumber(Right) - rect.GetNumber(0) == 0
        && rect.GetNumber(Top) - rect.GetNumber(1) == 0;

    /// <summary>Checks an annotation's subtype against the types each part allows.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="types">The allowed types.</param>
    /// <param name="annotation">The annotation.</param>
    private static void CheckType(PdfAValidationContext context, PdfAAnnotationTypes types, PdfAAnnotation annotation)
    {
        var subtype = annotation.Dictionary.GetName(KnownName.Subtype);
        var inPdfA2 = types.PdfA2.Contains(subtype);
        var inPdfA4 = types.PdfA4.Contains(subtype);
        PdfAAnnotations.Require(context, types.PdfA1.Contains(subtype), PdfARule.AnnotationTypePdfA1, annotation);
        PdfAAnnotations.Require(context, inPdfA2, PdfARule.AnnotationTypePdfA2, annotation);

        // The generated rule names for plain PDF/A-4 and PDF/A-4f are crossed: AnnotationTypePdfA4 is the PDF/A-4f rule
        // (which also allows FileAttachment) and AnnotationTypePdfA4F the plain PDF/A-4 rule.
        PdfAAnnotations.Require(context, inPdfA4 || subtype == types.FileAttachment, PdfARule.AnnotationTypePdfA4, annotation);
        PdfAAnnotations.Require(context, inPdfA4, PdfARule.AnnotationTypePdfA4F, annotation);
        PdfAAnnotations.Require(context, inPdfA4 || types.PdfA4E.Contains(subtype), PdfARule.AnnotationTypePdfA4E, annotation);
    }

    /// <summary>Checks an annotation's <c>/F</c> flags.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="annotation">The annotation.</param>
    private static void CheckFlags(PdfAValidationContext context, PdfAAnnotation annotation)
    {
        var dictionary = annotation.Dictionary;
        var value = dictionary.Get(KnownName.F);
        var present = value.IsNumber;
        var flags = value.AsInteger();
        var printable = (flags & PrintFlag) == PrintFlag;
        var popup = dictionary.IsName(KnownName.Subtype, KnownName.Popup);
        PdfAAnnotations.Require(context, present && printable && (flags & ForbiddenFlagsPdfA1) == 0, PdfARule.AnnotationFlagsPdfA1, annotation);
        PdfAAnnotations.Require(context, !present || (printable && (flags & ForbiddenFlags) == 0), PdfARule.AnnotationFlags, annotation);
        PdfAAnnotations.Require(context, popup || present, PdfARule.AnnotationFlagsPresent, annotation);
        // ISO 19005-4 also exempts annotations with no width and height. The current veraPDF profiles do too; the
        // profile bundled with veraPDF 1.30.3 predates that and still fails them.
        PdfAAnnotations.Require(context, popup || present || IsPoint(dictionary), PdfARule.AnnotationFlagsPresentPdfA4, annotation);
    }
}
