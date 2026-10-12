// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on colour, output intents and images. The checks walk the document as veraPDF does: the output intents
/// first, then each page's content, forms, patterns, glyphs and annotation appearances, checking each colour space,
/// ICC profile, image and halftone they use.
/// </summary>
internal static class PdfAColourRules
{
    /// <summary>The PDF/A part whose pages may carry their own output intents.</summary>
    private const int Part4 = 4;

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.RenderingIntentName,
        PdfARule.IccInputProfilePdfA1,
        PdfARule.IccInputProfile,
        PdfARule.IccOutputProfilePdfA1,
        PdfARule.IccOutputProfile,
        PdfARule.IccProfileComponents,
        PdfARule.JpxBitDepth,
        PdfARule.JpxEnumeratedColorSpace,
        PdfARule.JpxColorMethod,
        PdfARule.JpxColorSpecifications,
        PdfARule.JpxChannels,
        PdfARule.OutputIntentSameProfile,
        PdfARule.DeviceCmykPdfA4,
        PdfARule.DeviceCmyk,
        PdfARule.DeviceGray,
        PdfARule.DeviceGrayPdfA4,
        PdfARule.DeviceNColorants,
        PdfARule.DeviceNComponents,
        PdfARule.DeviceNComponentsPdfA1,
        PdfARule.DeviceRgbPdfA4,
        PdfARule.DeviceRgb,
        PdfARule.HalftoneName,
        PdfARule.HalftoneType,
        PdfARule.HalftoneTransfer,
        PdfARule.IccCmykOverprintProfile,
        PdfARule.IccCmykOverprintMode,
        PdfARule.MaskBitsPerComponent,
        PdfARule.OutputIntentProfileReferencePdfX,
        PdfARule.OutputIntentProfileReference,
        PdfARule.SeparationConsistent,
        PdfARule.ImageInterpolate,
        PdfARule.ImageAlternates,
        PdfARule.ImageOpi,
        PdfARule.ImageBitsPerComponent,
        PdfARule.ImageBitsPerComponentPdfA1,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var state = new PdfAColourState(context);
        var intents = context.Document.Catalog.GetArray(state.Names.OutputIntents);
        if (context.Part == Part4)
        {
            state.DocumentOutputSpace = PdfAOutputIntentChecks.OutputSpace(state, intents);
        }

        if (intents is not null)
        {
            PdfAOutputIntentChecks.Check(state, intents);
        }

        for (var index = 0; index < context.Document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            VisitPage(state, PdfDocumentPages.GetPage(context.Document, index));
        }
    }

    /// <summary>Visits a page: its output intents, its content and annotations inside its blending space, then its group's colour space.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="page">The page.</param>
    private static void VisitPage(PdfAColourState state, PdfPage page)
    {
        VisitPageIntents(state, page.Dictionary);
        var scope = new PdfAColourResourceScope(page.Resources, null);
        var group = page.Dictionary.GetDictionary(KnownName.Group);
        var blending = PdfATransparency.PageBlendingSpace(state, group);
        if (blending is not null)
        {
            PdfATransparency.Enter(state, blending);
        }

        PdfAContentWalk.WalkPage(state, page.Dictionary.GetRaw(KnownName.Contents), scope);
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            PdfAFormWalk.VisitAppearances(state, annotation, scope);
            PdfAThreeDChecks.Visit(state, annotation, scope);
        }

        if (blending is not null)
        {
            PdfATransparency.Enter(state, null);
        }

        PdfATransparency.VisitGroupSpace(state, page.Dictionary.GetRaw(KnownName.Group), page.Resources);
    }

    /// <summary>Visits a page's own output intents, which only PDF/A-4 reads.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="page">The page dictionary.</param>
    private static void VisitPageIntents(PdfAColourState state, PdfDictionary page)
    {
        if (state.Context.Part != Part4)
        {
            return;
        }

        var intents = page.GetArray(state.Names.OutputIntents);
        state.PageOutputSpace = PdfAOutputIntentChecks.OutputSpace(state, intents);
        if (intents is { Count: > 0 })
        {
            PdfAOutputIntentChecks.Check(state, intents);
        }
    }
}
