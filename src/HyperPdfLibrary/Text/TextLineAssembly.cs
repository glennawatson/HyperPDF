// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Turns the runs of one line into characters. Runs are put in order along the line's direction; word spaces are
/// generated from the gap between a glyph's advance end and the next glyph's origin (see <see cref="TextSpacing"/>);
/// glyphs drawn twice at nearly the same place, as fake bold does, are kept once; and /ActualText replaces the glyphs it covers.
/// </summary>
internal static class TextLineAssembly
{
    /// <summary>The number of runs back that are compared with a run for being a repeat of it.</summary>
    private const int RunLookBack = 6;

    /// <summary>The number of glyphs back that are compared with a glyph for being a repeat of it.</summary>
    private const int GlyphLookBack = 12;

    /// <summary>The share of the em within which a repeated glyph or run counts as drawn twice.</summary>
    private const float RepeatShare = 0.12F;

    /// <summary>The size of the buffer for one character's decomposition.</summary>
    private const int PieceBuffer = TextUnicode.MaxDecomposition;

    /// <summary>The code recorded for characters that no glyph produced.</summary>
    private const int NoCode = -1;

    /// <summary>Builds the characters of a line and adds them to the page.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="firstRun">The first run of the line.</param>
    /// <param name="runCount">The number of runs in the line.</param>
    internal static void AppendLine(TextPageBuildState state, int firstRun, int runCount)
    {
        state.LineChars.Clear();
        state.Recent.Clear();
        state.Order.Clear();
        state.HasPrevious = false;
        state.GroupCount = 0;
        state.LineAxis = state.Frames[firstRun].Axis;
        var raw = state.Device.Options.Order == PdfTextOrder.Raw;
        for (var i = firstRun; i < firstRun + runCount; i++)
        {
            if (raw || !IsRepeatedRun(state, i))
            {
                state.Order.Add(i);
            }
        }

        if (!raw)
        {
            SortAlongLine(state);
        }

        foreach (var run in CollectionsMarshal.AsSpan(state.Order))
        {
            AppendRun(state, run);
        }

        TextLineOrder.Complete(state);
    }

    /// <summary>Puts the line's runs in order of their start along the line's direction; runs at one place keep content order.</summary>
    /// <param name="state">The build state.</param>
    private static void SortAlongLine(TextPageBuildState state)
    {
        var order = CollectionsMarshal.AsSpan(state.Order);
        var axis = state.LineAxis;
        for (var i = 1; i < order.Length; i++)
        {
            var value = order[i];
            var key = Vector2.Dot(state.Frames[value].Start, axis);
            var j = i - 1;
            while (j >= 0 && Vector2.Dot(state.Frames[order[j]].Start, axis) > key)
            {
                order[j + 1] = order[j];
                j--;
            }

            order[j + 1] = value;
        }
    }

    /// <summary>Determines whether a run shows the same glyphs as a recent run of the line, at nearly the same place.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The index of the run.</param>
    /// <returns><see langword="true"/> when it is a repeat.</returns>
    private static bool IsRepeatedRun(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        var frame = state.Frames[index];
        var limit = Math.Max(0, state.Order.Count - RunLookBack);
        for (var k = state.Order.Count - 1; k >= limit; k--)
        {
            var earlier = state.Order[k];
            var other = state.Runs[earlier];
            if (!ReferenceEquals(other.Font, run.Font) || other.GlyphCount != run.GlyphCount || other.Marks.Id != run.Marks.Id)
            {
                continue;
            }

            if (IsNear(state.Frames[earlier].Start, frame.Start, frame.SafeEm) && SameCodes(state, other, run))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether two runs show the same character codes.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="first">The first run.</param>
    /// <param name="second">The second run.</param>
    /// <returns><see langword="true"/> when every code matches.</returns>
    private static bool SameCodes(TextPageBuildState state, in TextRun first, in TextRun second)
    {
        var glyphs = CollectionsMarshal.AsSpan(state.Glyphs);
        for (var i = 0; i < first.GlyphCount; i++)
        {
            if (glyphs[first.FirstGlyph + i].Code != glyphs[second.FirstGlyph + i].Code)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether two points are within the repeat distance of each other on both axes.</summary>
    /// <param name="first">The first point.</param>
    /// <param name="second">The second point.</param>
    /// <param name="em">The em.</param>
    /// <returns><see langword="true"/> when they are near.</returns>
    private static bool IsNear(Vector2 first, Vector2 second, float em)
    {
        var tolerance = RepeatShare * em;
        return MathF.Abs(first.X - second.X) <= tolerance && MathF.Abs(first.Y - second.Y) <= tolerance;
    }

    /// <summary>Adds the characters of one run.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The index of the run.</param>
    private static void AppendRun(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        if (run.Marks.Id != 0)
        {
            if (state.PlacedSequences.Add(run.Marks.Id))
            {
                AppendReplacementText(state, index, run);
            }

            return;
        }

        var glyphs = CollectionsMarshal.AsSpan(state.Glyphs).Slice(run.FirstGlyph, run.GlyphCount);
        foreach (var glyph in glyphs)
        {
            AppendGlyph(state, index, run, glyph);
        }
    }

    /// <summary>Adds the glyph's characters, a generated space before it when the gap calls for one.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The index of the run.</param>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph.</param>
    private static void AppendGlyph(TextPageBuildState state, int index, in TextRun run, in TextGlyph glyph)
    {
        var frame = state.Frames[index];
        var origin = TextGeometry.Transform(run.Matrix, glyph.Origin);
        if (state.Device.Options.Order != PdfTextOrder.Raw && IsRepeatedGlyph(state, run, glyph.Code, origin, frame.Em))
        {
            return;
        }

        var text = CollectionsMarshal.AsSpan(state.Unicode).Slice(glyph.UnicodeStart, glyph.UnicodeLength);
        var pen = TextGeometry.Transform(run.Matrix, PenPoint(run, glyph.Pen));
        if (state.HasPrevious && NeedsSpace(state, pen, text))
        {
            AddGeneratedSpace(state, index);
        }

        AddGlyphChars(state, index, run, glyph, text);
        state.Recent.Add(new(run.Font, glyph.Code, origin, frame.Em));
        state.HasPrevious = true;
        state.PreviousEnd = TextGeometry.Transform(run.Matrix, PenPoint(run, glyph.Pen + glyph.Advance));
        state.PreviousEm = frame.Em;
        state.PreviousFont = run.Font;
    }

    /// <summary>Starts a new glyph group in the line being built.</summary>
    /// <param name="state">The build state.</param>
    /// <returns>The group number.</returns>
    private static int NextGroup(TextPageBuildState state)
    {
        var group = state.GroupCount;
        state.GroupCount = group + 1;
        return group;
    }

    /// <summary>Gets a point on a run's pen line.</summary>
    /// <param name="run">The run.</param>
    /// <param name="along">The distance along the writing direction.</param>
    /// <returns>The point in run space.</returns>
    private static Vector2 PenPoint(in TextRun run, float along) => run.Font.IsVertical ? new(0, along) : new(along, 0);

    /// <summary>Determines whether a glyph repeats one of the last few glyphs of the line.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="run">The glyph's run.</param>
    /// <param name="code">The code.</param>
    /// <param name="origin">The glyph origin in user space.</param>
    /// <param name="em">The em.</param>
    /// <returns><see langword="true"/> when it is a repeat.</returns>
    private static bool IsRepeatedGlyph(TextPageBuildState state, in TextRun run, int code, Vector2 origin, float em)
    {
        var limit = Math.Max(0, state.Recent.Count - GlyphLookBack);
        for (var k = state.Recent.Count - 1; k >= limit; k--)
        {
            var recent = state.Recent[k];
            var tolerance = MathF.Max(MathF.Abs(em), MathF.Abs(recent.Em));

            // Text of size zero all sits at one point, so its glyphs are the text itself and never a second pass of fake bold.
            if (tolerance > 0 && recent.Code == code && ReferenceEquals(recent.Font, run.Font) && IsNear(recent.Origin, origin, tolerance))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the gap before a glyph is a word space.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="pen">The glyph's pen position in user space.</param>
    /// <param name="text">The glyph's text.</param>
    /// <returns><see langword="true"/> when a space should be generated.</returns>
    private static bool NeedsSpace(TextPageBuildState state, Vector2 pen, ReadOnlySpan<char> text)
    {
        var em = MathF.Abs(state.PreviousEm);
        var gap = Vector2.Dot(pen - state.PreviousEnd, state.LineAxis);
        if (em <= 0 || gap <= TextSpacing.NeverSpaceEm * em || state.PreviousFont is null)
        {
            return false;
        }

        if (gap <= TextSpacing.ThresholdEm(state, state.PreviousFont) * em)
        {
            return false;
        }

        return !EndsWithSpace(state) && !StartsWithSpace(text) && !BothCjk(state, text);
    }

    /// <summary>Determines whether the line so far ends with a space.</summary>
    /// <param name="state">The build state.</param>
    /// <returns><see langword="true"/> when the last character is a space.</returns>
    private static bool EndsWithSpace(TextPageBuildState state) => state.LineChars.Count > 0 && state.LineChars[^1] is { Unicode: ' ', InText: true };

    /// <summary>Determines whether a glyph's text starts with a space.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool StartsWithSpace(ReadOnlySpan<char> text) => !text.IsEmpty && text[0] == ' ';

    /// <summary>Determines whether a gap lies between two characters of scripts that are written without spaces.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="text">The text after the gap.</param>
    /// <returns><see langword="true"/> when both sides are CJK.</returns>
    private static bool BothCjk(TextPageBuildState state, ReadOnlySpan<char> text) =>
        !text.IsEmpty && state.LineChars.Count > 0 && TextUnicode.IsCjk(text[0]) && TextUnicode.IsCjk(state.LineChars[^1].Unicode);

    /// <summary>Adds a space the extractor generated at the end of the previous glyph.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The run of the glyph after the space.</param>
    private static void AddGeneratedSpace(TextPageBuildState state, int index)
    {
        var point = state.PreviousEnd;
        var box = new PdfRectangle(point.X, point.Y, point.X, point.Y);
        state.LineChars.Add(new(PdfTextCharKind.Generated, NoCode, ' ', point, box, box, index, NextGroup(state), state.LineIndex, true, 0));
    }

    /// <summary>Adds the characters one glyph shows.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The index of the run.</param>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="text">The glyph's text.</param>
    private static void AddGlyphChars(TextPageBuildState state, int index, in TextRun run, in TextGlyph glyph, ReadOnlySpan<char> text)
    {
        var group = NextGroup(state);
        var origin = TextGeometry.Transform(run.Matrix, glyph.Origin);
        var outline = TextGeometry.IsEmpty(glyph.Outline) ? glyph.Cell : glyph.Outline;
        var loose = TextGeometry.Union(glyph.Cell, outline);
        var vertical = run.Font.IsVertical;
        if (text.IsEmpty)
        {
            var value = (char)glyph.Code;
            var box = TextGeometry.TransformRect(run.Matrix, outline);
            state.LineChars.Add(new(PdfTextCharKind.NotUnicode, glyph.Code, value, origin, box, TextGeometry.TransformRect(run.Matrix, loose), index, group, state.LineIndex, false, 0));
            return;
        }

        var total = CountPieces(text);
        var position = 0;
        Span<char> pieces = stackalloc char[PieceBuffer];
        foreach (var value in text)
        {
            var count = TextUnicode.Decompose(value, pieces);
            var kind = count > 0 ? PdfTextCharKind.Piece : PdfTextCharKind.Normal;
            for (var i = 0; i < Math.Max(count, 1); i++)
            {
                var character = count > 0 ? pieces[i] : value;
                var box = TextGeometry.TransformRect(run.Matrix, Slice(outline, position, total, vertical));
                var looseBox = TextGeometry.TransformRect(run.Matrix, Slice(loose, position, total, vertical));
                AddCharacter(state, new(kind, glyph.Code, character, origin, box, looseBox, index, group, state.LineIndex, !TextUnicode.IsHidden(character), 0));
                position++;
            }
        }
    }

    /// <summary>Counts the characters a glyph's text gives once presentation forms are split.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The count.</returns>
    private static int CountPieces(ReadOnlySpan<char> text)
    {
        var total = 0;
        Span<char> pieces = stackalloc char[PieceBuffer];
        foreach (var value in text)
        {
            total += Math.Max(TextUnicode.Decompose(value, pieces), 1);
        }

        return total;
    }

    /// <summary>Adds a character, leaving out a space that follows another space.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="character">The character.</param>
    private static void AddCharacter(TextPageBuildState state, in TextBuildChar character)
    {
        if (character is { Unicode: ' ', InText: true } && EndsWithSpace(state))
        {
            return;
        }

        state.LineChars.Add(character);
    }

    /// <summary>Gets the part of a rectangle that one character of a multi-character glyph covers.</summary>
    /// <param name="rectangle">The glyph rectangle in run space.</param>
    /// <param name="position">The character's position among the glyph's characters.</param>
    /// <param name="total">The number of characters.</param>
    /// <param name="vertical">Whether the run writes vertically, which runs top to bottom.</param>
    /// <returns>The part in run space.</returns>
    private static PdfRectangle Slice(in PdfRectangle rectangle, int position, int total, bool vertical)
    {
        if (total <= 1)
        {
            return rectangle;
        }

        if (vertical)
        {
            var height = (rectangle.Top - rectangle.Bottom) / total;
            return new(rectangle.Left, rectangle.Top - ((position + 1) * height), rectangle.Right, rectangle.Top - (position * height));
        }

        var width = (rectangle.Right - rectangle.Left) / total;
        return new(rectangle.Left + (position * width), rectangle.Bottom, rectangle.Left + ((position + 1) * width), rectangle.Top);
    }

    /// <summary>Adds the /ActualText of a run in place of its glyphs, spread along the run's extent.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="index">The index of the run.</param>
    /// <param name="run">The run.</param>
    private static void AppendReplacementText(TextPageBuildState state, int index, in TextRun run)
    {
        var glyphs = CollectionsMarshal.AsSpan(state.Glyphs).Slice(run.FirstGlyph, run.GlyphCount);
        var extent = glyphs[0].Cell;
        foreach (var glyph in glyphs)
        {
            extent = TextGeometry.Union(extent, glyph.Cell);
        }

        var text = run.Marks.ActualText;
        var vertical = run.Font.IsVertical;
        var origin = TextGeometry.Transform(run.Matrix, glyphs[0].Origin);
        for (var i = 0; i < text.Length; i++)
        {
            var box = TextGeometry.TransformRect(run.Matrix, Slice(extent, i, text.Length, vertical));
            AddCharacter(state, new(PdfTextCharKind.ActualText, NoCode, text[i], origin, box, box, index, NextGroup(state), state.LineIndex, !TextUnicode.IsHidden(text[i]), 0));
        }

        var last = glyphs[^1];
        state.HasPrevious = true;
        state.PreviousEnd = TextGeometry.Transform(run.Matrix, PenPoint(run, last.Pen + last.Advance));
        state.PreviousEm = state.Frames[index].Em;
        state.PreviousFont = run.Font;
    }
}
