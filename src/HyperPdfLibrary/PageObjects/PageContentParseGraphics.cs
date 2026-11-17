// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Tracks graphics state, paths and clipping while parsing content.</summary>
internal static class PageContentParseGraphics
{
    /// <summary>Handles <c>q</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSave(PageContentParseState self, ref ContentReader reader) => self.Stack.Add(self.State);

    /// <summary>Handles <c>Q</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpRestore(PageContentParseState self, ref ContentReader reader)
    {
        if (self.Stack.Count == 0)
        {
            self.UnderflowCount++;
            return;
        }

        var last = self.Stack.Count - 1;
        self.State = self.Stack[last];
        self.Stack.RemoveAt(last);
    }

    /// <summary>Handles <c>cm</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpConcatMatrix(PageContentParseState self, ref ContentReader reader)
    {
        var matrix = new Matrix3x2(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand),
            reader.Number(PageContentParse.FourthOperand),
            reader.Number(PageContentParse.FifthOperand),
            reader.Number(PageContentParse.SixthOperand));
        self.State.Ctm = matrix * self.State.Ctm;
    }

    /// <summary>Handles <c>w</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetLineWidth(PageContentParseState self, ref ContentReader reader) => self.State.LineWidth = Math.Max(0, reader.Number(0));

    /// <summary>Handles <c>gs</c>: follows the opacity and blend mode, so an edit can put them back after it changes them.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetGraphicsState(PageContentParseState self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        if (PageContentParseObjects.FindResource(self, KnownName.ExtGState, name).AsDictionary() is not { } dictionary)
        {
            return;
        }

        if (dictionary.Get(KnownName.CA).IsNumber)
        {
            self.State.StrokeAlpha = Math.Clamp(dictionary.GetSingle(KnownName.CA), 0F, 1F);
        }

        if (dictionary.Get(KnownName.FillAlpha).IsNumber)
        {
            self.State.FillAlpha = Math.Clamp(dictionary.GetSingle(KnownName.FillAlpha), 0F, 1F);
        }

        var blend = dictionary.Get(KnownName.BM);
        if (blend.AsArray() is { Count: > 0 } list)
        {
            blend = list.Get(0);
        }

        if (blend.Kind == PdfKind.Name)
        {
            self.State.BlendMode = blend.AsName();
        }
    }

    /// <summary>Handles <c>m</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpMoveTo(
        PageContentParseState self,
        ref ContentReader reader) =>
        PageContentParseGraphics.AddSegment(
            self,
            new(
                PdfPathSegmentKind.MoveTo,
                reader.Number(0),
                reader.Number(1),
                0,
                0,
                0,
                0));

    /// <summary>Handles <c>l</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpLineTo(
        PageContentParseState self,
        ref ContentReader reader) =>
        PageContentParseGraphics.AddSegment(
            self,
            new(
                PdfPathSegmentKind.LineTo,
                reader.Number(0),
                reader.Number(1),
                0,
                0,
                0,
                0));

    /// <summary>Handles <c>c</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpCurveTo(PageContentParseState self, ref ContentReader reader)
    {
        var x1 = reader.Number(0);
        var y1 = reader.Number(1);
        PageContentParseGraphics.AddSegment(
            self,
            new(
                PdfPathSegmentKind.CurveTo,
                x1,
                y1,
                reader.Number(PageContentParse.ThirdOperand),
                reader.Number(PageContentParse.FourthOperand),
                reader.Number(PageContentParse.FifthOperand),
                reader.Number(PageContentParse.SixthOperand)));
    }

    /// <summary>Handles <c>v</c>: the first control point is the current point.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCurveToV(
        PageContentParseState self,
        ref ContentReader reader) =>
        PageContentParseGraphics.AddSegment(
            self,
            new(
                PdfPathSegmentKind.CurveTo,
                self.Current.X,
                self.Current.Y,
                reader.Number(0),
                reader.Number(1),
                reader.Number(PageContentParse.ThirdOperand),
                reader.Number(PageContentParse.FourthOperand)));

    /// <summary>Handles <c>y</c>: the second control point is the end point.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpCurveToY(PageContentParseState self, ref ContentReader reader)
    {
        var endX = reader.Number(PageContentParse.ThirdOperand);
        var endY = reader.Number(PageContentParse.FourthOperand);
        PageContentParseGraphics.AddSegment(self, new(PdfPathSegmentKind.CurveTo, reader.Number(0), reader.Number(1), endX, endY, endX, endY));
    }

    /// <summary>Handles <c>h</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpClosePath(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.AddSegment(self, new(PdfPathSegmentKind.Close, 0, 0, 0, 0, 0, 0));

    /// <summary>Handles <c>re</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpRectangle(
        PageContentParseState self,
        ref ContentReader reader) =>
        PageContentParseGraphics.AddSegment(
            self,
            new(
                PdfPathSegmentKind.Rectangle,
                reader.Number(0),
                reader.Number(1),
                reader.Number(PageContentParse.ThirdOperand),
                reader.Number(PageContentParse.FourthOperand),
                0,
                0));

    /// <summary>Handles <c>f</c> and <c>F</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFill(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.Fill, false, false);

    /// <summary>Handles <c>f*</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillEvenOdd(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.Fill, true, false);

    /// <summary>Handles <c>S</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpStroke(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.Stroke, false, false);

    /// <summary>Handles <c>s</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseStroke(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.Stroke, false, true);

    /// <summary>Handles <c>B</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillStroke(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.FillStroke, false, false);

    /// <summary>Handles <c>B*</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillStrokeEvenOdd(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.FillStroke, true, false);

    /// <summary>Handles <c>b</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseFillStroke(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.FillStroke, false, true);

    /// <summary>Handles <c>b*</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseFillStrokeEvenOdd(PageContentParseState self, ref ContentReader reader) => PageContentParseGraphics.PaintPath(self, PdfPathPaintMode.FillStroke, true, true);

    /// <summary>Handles <c>n</c>: ends the path, applying a pending clip.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpEndPath(PageContentParseState self, ref ContentReader reader)
    {
        // A path that only clips is not an object, so the clip remembers where its operators are, to be moved with the object it clips.
        PageContentParseGraphics.ApplyPendingClip(self, new(self.PathStart, self.OperatorEnd));
        PageContentParseGraphics.ResetPath(self);
    }

    /// <summary>Handles <c>W</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpClip(PageContentParseState self, ref ContentReader reader) => self.PendingClip = PdfClipMode.NonZero;

    /// <summary>Handles <c>W*</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpClipEvenOdd(PageContentParseState self, ref ContentReader reader) => self.PendingClip = PdfClipMode.EvenOdd;

    /// <summary>Adds a segment to the path being built.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "segment">The segment.</param>
    internal static void AddSegment(PageContentParseState state, PdfPathSegment segment)
    {
        if (state.PathStart < 0)
        {
            state.PathStart = state.OperatorStart;
        }

        state.Segments.Add(segment);
        var point = new Vector2(segment.X1, segment.Y1);
        switch (segment.Kind)
        {
            case PdfPathSegmentKind.MoveTo or PdfPathSegmentKind.Rectangle:
                {
                    state.Current = point;
                    state.SubpathStart = point;
                    break;
                }

            case PdfPathSegmentKind.LineTo:
                {
                    state.Current = point;
                    break;
                }

            case PdfPathSegmentKind.CurveTo:
                {
                    state.Current = new(segment.X3, segment.Y3);
                    break;
                }

            default:
                {
                    state.Current = state.SubpathStart;
                    break;
                }
        }
    }

    /// <summary>Paints the path as an object and applies its clip.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "mode">How the path is painted.</param>
    /// <param name = "evenOdd">Whether the fill uses the even-odd rule.</param>
    /// <param name = "close">Whether the operator closed the path first.</param>
    internal static void PaintPath(PageContentParseState state, PdfPathPaintMode mode, bool evenOdd, bool close)
    {
        if (state.PathStart < 0 || state.Segments.Count == 0)
        {
            PageContentParseGraphics.ResetPath(state);
            return;
        }

        var segments = state.Segments.ToArray();
        var width = mode == PdfPathPaintMode.Fill ? 0 : state.State.LineWidth;
        var item = new PdfPathObject { Segments = segments, PaintMode = mode, EvenOddFill = evenOdd, ClosesPath = close, Clip = state.PendingClip, };
        PageContentParseObjects.Add(state, item, PathBounds.Measure(segments, state.State.Ctm, width), new(state.PathStart, state.OperatorEnd));
        PageContentParseGraphics.ApplyPendingClip(state, default);
        PageContentParseGraphics.ResetPath(state);
    }

    /// <summary>Adds the pending clip to the state, after the path it came with was painted.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "source">Where the operators of a path that was only a clip are, or empty when the path was painted too.</param>
    internal static void ApplyPendingClip(PageContentParseState state, ByteRange source)
    {
        if (state.PendingClip == PdfClipMode.None || state.Segments.Count == 0)
        {
            return;
        }

        var segments = state.Segments.ToArray();
        var bounds = PathBounds.Measure(segments, state.State.Ctm, 0);
        var parent = state.State.Clip;
        var clipped = parent is null ? bounds : parent.Bounds.Intersect(bounds);
        var path = new PdfClipPath(segments, state.State.Ctm, state.PendingClip == PdfClipMode.EvenOdd, bounds);
        state.State.Clip = new(parent, path, clipped, source);
    }

    /// <summary>Forgets the path being built.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    internal static void ResetPath(PageContentParseState state)
    {
        state.Segments.Clear();
        state.PathStart = -1;
        state.PendingClip = PdfClipMode.None;
    }
}
