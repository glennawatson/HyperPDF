// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on annotation appearance dictionaries and on widget annotations (ISO 19005-1 6.5.3, 6.6 and 6.9;
/// ISO 19005-2/3/4 6.3.3 and 6.4.1).
/// </summary>
internal static class PdfAAppearanceRules
{
    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.AnnotationAppearancePdfA2,
        PdfARule.AnnotationAppearancePdfA4,
        PdfARule.AnnotationOnlyNormalAppearance,
        PdfARule.AnnotationNormalAppearanceStream,
        PdfARule.AnnotationButtonAppearanceDictionary,
        PdfARule.WidgetAppearance,
        PdfARule.WidgetAction,
        PdfARule.WidgetActions,
        PdfARule.WidgetAdditionalActions,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var projection = context.Document.Objects.Names.Intern("Projection"u8);
        foreach (var annotation in PdfAAnnotations.Read(context))
        {
            var dictionary = annotation.Dictionary;
            var subtype = dictionary.GetName(KnownName.Subtype);
            var appearance = dictionary.Get(KnownName.AP);
            var hasAppearance = appearance.Kind == PdfKind.Dictionary;
            var exempt = subtype.Is(KnownName.Popup) || subtype.Is(KnownName.Link) || PdfAAnnotationRules.IsPoint(dictionary);
            PdfAAnnotations.Require(context, exempt || hasAppearance, PdfARule.AnnotationAppearancePdfA2, annotation);
            PdfAAnnotations.Require(context, exempt || subtype == projection || hasAppearance, PdfARule.AnnotationAppearancePdfA4, annotation);
            var widget = subtype.Is(KnownName.Widget);
            var button = widget && IsButton(dictionary);
            CheckEntries(context, annotation, hasAppearance ? appearance.AsDictionary() : null, button);
            if (widget)
            {
                CheckWidget(context, annotation, hasAppearance);
            }
        }
    }

    /// <summary>Checks the entries of an appearance dictionary: only <c>/N</c>, of the right kind.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="appearance">The appearance dictionary, or <see langword="null"/>.</param>
    /// <param name="button">Whether the annotation is a button widget.</param>
    private static void CheckEntries(PdfAValidationContext context, PdfAAnnotation annotation, PdfDictionary? appearance, bool button)
    {
        if (appearance is null)
        {
            return;
        }

        var onlyNormal = appearance.Count == 1 && appearance.GetKeyAt(0).Is(KnownName.N);
        PdfAAnnotations.Require(context, onlyNormal, PdfARule.AnnotationOnlyNormalAppearance, annotation);
        if (!onlyNormal)
        {
            return;
        }

        var normal = appearance.Get(KnownName.N);
        PdfAAnnotations.Require(context, button || normal.Kind == PdfKind.Stream, PdfARule.AnnotationNormalAppearanceStream, annotation);
        var subdictionary = normal.Kind == PdfKind.Dictionary && HoldsAStream(normal.AsDictionary()!);
        PdfAAnnotations.Require(context, !button || subdictionary, PdfARule.AnnotationButtonAppearanceDictionary, annotation);
    }

    /// <summary>Checks a widget annotation's appearance and actions.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="annotation">The widget annotation.</param>
    /// <param name="hasAppearance">Whether it has an appearance dictionary.</param>
    private static void CheckWidget(PdfAValidationContext context, PdfAAnnotation annotation, bool hasAppearance)
    {
        var dictionary = annotation.Dictionary;
        var action = dictionary.ContainsKey(KnownName.A);
        var additional = dictionary.ContainsKey(KnownName.AA);
        PdfAAnnotations.Require(context, hasAppearance, PdfARule.WidgetAppearance, annotation);
        PdfAAnnotations.Require(context, !action, PdfARule.WidgetAction, annotation);
        PdfAAnnotations.Require(context, !action && !additional, PdfARule.WidgetActions, annotation);
        PdfAAnnotations.Require(context, !additional, PdfARule.WidgetAdditionalActions, annotation);
    }

    /// <summary>Determines whether a widget's field type, inherited through <c>/Parent</c>, is a button.</summary>
    /// <param name="widget">The widget annotation.</param>
    /// <returns><see langword="true"/> for a button.</returns>
    private static bool IsButton(PdfDictionary widget)
    {
        var node = widget;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxNesting; depth++)
        {
            var type = node.Get(KnownName.FT);
            if (!type.IsNull)
            {
                return type.IsName(KnownName.Btn);
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return false;
    }

    /// <summary>
    /// Determines whether an appearance subdictionary holds at least one appearance stream. veraPDF accepts a subdictionary
    /// with any stream in it, and rejects an empty one.
    /// </summary>
    /// <param name="subdictionary">The subdictionary.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HoldsAStream(PdfDictionary subdictionary)
    {
        for (var i = 0; subdictionary.Owner is { } store && i < subdictionary.Count; i++)
        {
            if (StoreReading.Resolve(store, subdictionary.GetValueAt(i)).Kind == PdfKind.Stream)
            {
                return true;
            }
        }

        return false;
    }
}
