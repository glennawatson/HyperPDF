// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A caret annotation (ISO 32000-2 12.5.6.11): a filled "^" in the colour inside <c>/Rect</c> less <c>/RD</c>. The
/// paragraph symbol of <c>/Sy /P</c> is not drawn, so the caret alone shows.
/// </summary>
/// <param name="Caret">The caret's own rectangle.</param>
/// <param name="Rect">The annotation rectangle, which the appearance is fitted to.</param>
/// <param name="Color">The fill colour, black when absent.</param>
[DebuggerDisplay("CaretPlan: {Caret}")]
internal readonly record struct CaretPlan(PdfRectangle Caret, PdfRectangle Rect, PaintColor Color) : IAppearancePlan<CaretPlan>
{
    /// <summary>How far, as a fraction of the width, the flanks' control points sit from the foot and the apex.</summary>
    private const float FlankReach = 0.3F;

    /// <summary>How far up, as a fraction of the height, the flanks' first control points sit.</summary>
    private const float FootLift = 0.05F;

    /// <summary>How far down from the apex, as a fraction of the height, the flanks' second control points sit.</summary>
    private const float ApexDrop = 0.45F;

    /// <summary>How far up, as a fraction of the height, the base is notched.</summary>
    private const float NotchHeight = 0.3F;

    /// <inheritdoc/>
    public PdfRectangle Bounds => Rect;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out CaretPlan plan)
    {
        var rect = AppearanceRects.Read(annotation);
        var caret = AppearanceRects.ApplyDifferences(annotation, rect, rect);
        plan = new(caret, rect, PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.Black));
        return !rect.IsEmpty && !caret.IsEmpty;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        Color.SetFill(ref builder);

        // Two flanks rise to the apex and the base is notched upwards in the middle.
        var middle = (Caret.Left + Caret.Right) * AppearanceRects.Half;
        var reach = Caret.Width * FlankReach;
        var halfReach = reach * AppearanceRects.Half;
        var foot = Caret.Bottom + (Caret.Height * FootLift);
        var shoulder = Caret.Top - (Caret.Height * ApexDrop);
        var notch = Caret.Bottom + (Caret.Height * NotchHeight);
        builder.MoveTo(Caret.Left, Caret.Bottom);
        builder.CurveTo(Caret.Left + reach, foot, middle - halfReach, shoulder, middle, Caret.Top);
        builder.CurveTo(middle + halfReach, shoulder, Caret.Right - reach, foot, Caret.Right, Caret.Bottom);
        builder.CurveTo(Caret.Right - reach, notch, Caret.Left + reach, notch, Caret.Left, Caret.Bottom);
        builder.ClosePath();
        builder.Fill();
    }
}
