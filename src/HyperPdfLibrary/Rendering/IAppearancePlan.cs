// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// What drawing one kind of annotation needs, read once from the annotation dictionary into a small value and then
/// written into a pooled content buffer in one pass (ISO 32000-2 12.5.5 and 12.5.6). Plans are structs, so
/// <see cref="AnnotationAppearances"/> runs each plan type without boxing or an indirect call.
/// </summary>
/// <typeparam name="TSelf">The plan type.</typeparam>
internal interface IAppearancePlan<TSelf>
    where TSelf : struct, IAppearancePlan<TSelf>
{
    /// <summary>Gets the rectangle the appearance is fitted to, which can differ from <c>/Rect</c>, for example round text markup quadrilaterals.</summary>
    PdfRectangle Bounds { get; }

    /// <summary>Gets the blend mode the appearance is drawn with (ISO 32000-2 11.3.5).</summary>
    KnownName Blend { get; }

    /// <summary>Reads the plan from an annotation.</summary>
    /// <param name="annotation">The annotation dictionary.</param>
    /// <param name="plan">Receives the plan.</param>
    /// <returns><see langword="false"/> when the annotation lacks what the subtype needs or would show nothing.</returns>
    static abstract bool TryRead(PdfDictionary annotation, out TSelf plan);

    /// <summary>Writes the content that draws the annotation, after the graphics state selection.</summary>
    /// <param name="builder">The content.</param>
    void Emit(ref PdfContentBuilder builder);
}
