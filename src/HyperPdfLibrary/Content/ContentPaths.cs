// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Paths operations over its owned state.</summary>
internal static class ContentPaths
{
    /// <summary>The operand index of the second coordinate pair.</summary>
    internal const int SecondPair = 2;

    /// <summary>The operand index of the third coordinate pair.</summary>
    internal const int ThirdPair = 4;

    /// <summary>The operand index of the height of a rectangle.</summary>
    internal const int RectangleHeight = 3;

    /// <summary>Handles <c>m</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpMoveTo(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PathMove(self, reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>l</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpLineTo(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.PathPoints > 0)
        {
            ContentPaths.PathLine(self, reader.Number(0), reader.Number(1));
        }
    }

    /// <summary>Handles <c>c</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpCurveTo(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.PathPoints > 0)
        {
            ContentPaths.PathCurve(
                self,
                reader.Number(0),
                reader.Number(1),
                reader.Number(ContentPaths.SecondPair),
                reader.Number(ContentPaths.SecondPair + 1),
                reader.Number(ContentPaths.ThirdPair),
                reader.Number(ContentPaths.ThirdPair + 1));
        }
    }

    /// <summary>Handles <c>v</c>: the first control point is the current point.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpCurveToV(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.PathPoints > 0)
        {
            ContentPaths.PathCurve(self, self.Current.X, self.Current.Y, reader.Number(0), reader.Number(1), reader.Number(ContentPaths.SecondPair), reader.Number(ContentPaths.SecondPair + 1));
        }
    }

    /// <summary>Handles <c>y</c>: the second control point is the end point.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpCurveToY(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.PathPoints > 0)
        {
            ContentPaths.PathCurve(
                self,
                reader.Number(0),
                reader.Number(1),
                reader.Number(ContentPaths.SecondPair),
                reader.Number(ContentPaths.SecondPair + 1),
                reader.Number(ContentPaths.SecondPair),
                reader.Number(ContentPaths.SecondPair + 1));
        }
    }

    /// <summary>Handles <c>h</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpClosePath(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.PathPoints > 0)
        {
            self.Path.Close();
        }
    }

    /// <summary>Handles <c>re</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpRectangle(ContentInterpreter self, ref ContentReader reader)
    {
        var x = reader.Number(0);
        var y = reader.Number(1);
        var width = reader.Number(ContentPaths.SecondPair);
        var height = reader.Number(ContentPaths.RectangleHeight);
        ContentPaths.PathMove(self, x, y);
        ContentPaths.PathLine(self, x + width, y);
        ContentPaths.PathLine(self, x + width, y + height);
        ContentPaths.PathLine(self, x, y + height);
        self.Path.Close();
    }

    /// <summary>Handles <c>f</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFill(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, false, false, false);

    /// <summary>Handles <c>f*</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillEvenOdd(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, false, true, false);

    /// <summary>Handles <c>S</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpStroke(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, false, true, false, false);

    /// <summary>Handles <c>s</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseStroke(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, false, true, false, true);

    /// <summary>Handles <c>B</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillStroke(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, true, false, false);

    /// <summary>Handles <c>B*</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpFillStrokeEvenOdd(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, true, true, false);

    /// <summary>Handles <c>b</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseFillStroke(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, true, false, true);

    /// <summary>Handles <c>b*</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpCloseFillStrokeEvenOdd(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, true, true, true, true);

    /// <summary>Handles <c>n</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpEndPath(ContentInterpreter self, ref ContentReader reader) => ContentPaths.PaintPath(self, false, false, false, false);

    /// <summary>Handles <c>W</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpClip(ContentInterpreter self, ref ContentReader reader)
    {
        self.PendingClip = true;
        self.PendingClipEvenOdd = false;
    }

    /// <summary>Handles <c>W*</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpClipEvenOdd(ContentInterpreter self, ref ContentReader reader)
    {
        self.PendingClip = true;
        self.PendingClipEvenOdd = true;
    }

    /// <summary>Starts a subpath.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "x">The x coordinate.</param>
    /// <param name = "y">The y coordinate.</param>
    internal static void PathMove(ContentInterpreter self, float x, float y)
    {
        self.Path.MoveTo(x, y);
        self.Current = new(x, y);
        self.PathPoints++;
    }

    /// <summary>Adds a line.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "x">The end x.</param>
    /// <param name = "y">The end y.</param>
    internal static void PathLine(ContentInterpreter self, float x, float y)
    {
        self.Path.LineTo(x, y);
        self.Current = new(x, y);
        self.PathPoints++;
    }

    /// <summary>Adds a cubic curve.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "x1">The first control point x.</param>
    /// <param name = "y1">The first control point y.</param>
    /// <param name = "x2">The second control point x.</param>
    /// <param name = "y2">The second control point y.</param>
    /// <param name = "x3">The end x.</param>
    /// <param name = "y3">The end y.</param>
    internal static void PathCurve(ContentInterpreter self, float x1, float y1, float x2, float y2, float x3, float y3)
    {
        self.Path.CubicTo(x1, y1, x2, y2, x3, y3);
        self.Current = new(x3, y3);
        self.PathPoints++;
    }

    /// <summary>
    /// Empties a path builder and hands its commands over. A device that keeps no paths gets the interpreter's reused
    /// path, which shows the commands until the next one is added; any other device gets an independent copy.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "builder">The builder.</param>
    /// <returns>The path.</returns>
    internal static PdfPath TakePath(ContentInterpreter self, PdfPathBuilder builder) => self.LendsPaths ? builder.Lend(self.ScratchPath) : builder.Detach();

    /// <summary>Paints the current path, applies a pending clip, and starts a new path.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "fill">Whether to fill.</param>
    /// <param name = "stroke">Whether to stroke.</param>
    /// <param name = "evenOdd">Whether the fill uses the even-odd rule.</param>
    /// <param name = "close">Whether to close the path first.</param>
    internal static void PaintPath(ContentInterpreter self, bool fill, bool stroke, bool evenOdd, bool close)
    {
        if (self.PathPoints == 0)
        {
            self.PendingClip = false;
            return;
        }

        if (close)
        {
            self.Path.Close();
        }

        if (!self.PendingClip && (self.Hidden != 0 || (!fill && !stroke)))
        {
            // A path that is neither painted nor clipped to has nothing to draw, so it is dropped without making a copy.
            self.Path.Reset();
            self.PathPoints = 0;
            return;
        }

        var path = ContentPaths.TakePath(self, self.Path);
        self.PathPoints = 0;
        if (self.Hidden == 0)
        {
            ContentPaths.FillAndStroke(self, path, fill, stroke, evenOdd);
        }

        if (self.PendingClip)
        {
            self.Device.Clip(path, self.PendingClipEvenOdd, self.State.Ctm);
        }

        self.PendingClip = false;
    }

    /// <summary>Fills and strokes a path unless the colour paints nothing.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "path">The path.</param>
    /// <param name = "fill">Whether to fill.</param>
    /// <param name = "stroke">Whether to stroke.</param>
    /// <param name = "evenOdd">Whether the fill uses the even-odd rule.</param>
    internal static void FillAndStroke(ContentInterpreter self, PdfPath path, bool fill, bool stroke, bool evenOdd)
    {
        if (fill && stroke && ContentPaths.NeedsStrokeKnockout(self))
        {
            ContentPaths.FillAndStrokeKnockout(self, path, evenOdd);
            return;
        }

        if (fill && !self.State.Fill.PaintsNothing)
        {
            ContentPainting.Fill(self, path, evenOdd, ref self.State);
        }

        if (stroke && !self.State.Stroke.PaintsNothing)
        {
            ContentPainting.Stroke(self, path, ref self.State);
        }
    }

    /// <summary>Determines whether a fill and stroke pair must be drawn as a knockout group: both paint and the stroke is translucent.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <returns><see langword="true"/> when the stroke must replace the fill under it.</returns>
    internal static bool NeedsStrokeKnockout(ContentInterpreter self) => self.State.StrokeAlpha < 1 && self.State.FillAlpha > 0 && !self.State.Fill.PaintsNothing && !self.State.Stroke.PaintsNothing;

    /// <summary>
    /// Fills and strokes a path whose stroke is translucent. ISO 32000-2 11.7.4.4 makes the pair one knockout group: the stroke
    /// replaces the fill under it instead of showing it through, and the group takes the state's blend mode and soft mask.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "path">The path.</param>
    /// <param name = "evenOdd">Whether the fill uses the even-odd rule.</param>
    internal static void FillAndStrokeKnockout(ContentInterpreter self, PdfPath path, bool evenOdd)
    {
        var reach = (self.State.LineWidth * Math.Max(1, self.State.MiterLimit)) + 1;
        var local = path.Bounds;
        local = local.Inflate(reach, reach);
        var bounds = self.State.Ctm.MapRect(local);
        var group = new GroupInfo(true, true, 1, self.State.BlendMode, self.State.SoftMask, bounds, self.BlendSpace);
        var inner = self.State;
        inner.BlendMode = PdfBlendMode.Normal;
        inner.SoftMask = null;
        self.Device.BeginGroup(group);
        ContentPainting.Fill(self, path, evenOdd, ref inner);
        ContentPainting.Stroke(self, path, ref inner);
        self.Device.EndGroup(group);
    }
}
