// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on graphics, transparency and content streams. It walks every page's content, annotation appearances and
/// the forms, patterns and Type 3 glyphs they draw, as veraPDF does, and checks what that content uses.
/// </summary>
internal static class PdfAGraphicsRules
{
    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.BoundingBoxSize,
        PdfARule.BlendModeName,
        PdfARule.UndefinedOperator,
        PdfARule.GraphicsStateNesting,
        PdfARule.ThreeDStreamSubtype,
        PdfARule.ContentInheritedResources,
        PdfARule.ContentUndefinedResources,
        PdfARule.ExtGStateStrokeAlpha,
        PdfARule.ExtGStateFillAlpha,
        PdfARule.ExtGStateBlendModePdfA1,
        PdfARule.ExtGStateHalftoneOrigin,
        PdfARule.ExtGStateHalftonePhase,
        PdfARule.ExtGStateSoftMask,
        PdfARule.ExtGStateTransfer,
        PdfARule.ExtGStateTransfer2,
        PdfARule.TransparencyGroupPdfA1,
        PdfARule.FormPostScriptPdfA1,
        PdfARule.FormPostScript,
        PdfARule.FormOpi,
        PdfARule.FormReference,
        PdfARule.XObjectPostScript,
        PdfARule.XObjectOpiPdfA1,
        PdfARule.XObjectSoftMaskPdfA1,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var walk = new PdfAGraphicsWalk(context);
        var document = context.Document;
        for (var index = 0; index < document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            PdfAPageGraphics.Check(walk, PdfDocumentPages.GetPage(document, index));
        }
    }
}
