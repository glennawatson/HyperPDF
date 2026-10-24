// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Layout;

/// <summary>
/// Groups blobs of ink into lines of text. A blob joins the line it sits level with and is close to, so the lines of
/// separate columns stay separate. Blobs far taller than the page's typical letter, such as pictures and rules, are left out.
/// </summary>
public static class TextLineFinder
{
    /// <summary>The smallest height a blob may have to be counted when finding the typical letter height.</summary>
    private const int MinLetterHeight = 4;

    /// <summary>The share of the typical height below which a blob is only a mark, such as a dot or a speck.</summary>
    private const double MarkShare = 0.5;

    /// <summary>The number of typical heights a blob may stand from the line it joins.</summary>
    private const double GapHeights = 3.0;

    /// <summary>The number of typical heights above which a blob is a picture or a rule, not a letter.</summary>
    private const double PictureHeights = 6.0;

    /// <summary>The share of a blob's height that must sit within a line's rows for the blob to join the line.</summary>
    private const double JoinOverlap = 0.5;

    /// <summary>The number of typical heights of width above which a thin blob is a rule.</summary>
    private const double RuleWidthHeights = 40.0;

    /// <summary>The share of the typical height below which a wide blob is thin enough to be a rule.</summary>
    private const double RuleThinShare = 0.3;

    /// <summary>Finds the lines of text.</summary>
    /// <param name="components">The blobs of ink on the page.</param>
    /// <param name="lines">Receives the lines, from top to bottom.</param>
    /// <param name="scratch">The working memory.</param>
    public static void Find(List<ComponentBox> components, List<TextLine> lines, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(scratch);
        var typical = TypicalHeight(components, scratch);
        if (typical == 0)
        {
            return;
        }

        var letters = scratch.Letters;
        letters.Clear();
        foreach (var component in components)
        {
            if (IsLetterSized(component, typical))
            {
                letters.Add(component);
            }
        }

        letters.Sort(static (a, b) => a.Left.CompareTo(b.Left));
        var builders = scratch.LineBuilders;
        builders.Clear();
        var gap = GapHeights * typical;
        foreach (var letter in letters)
        {
            var best = BestLine(builders, letter, gap);
            if (best < 0)
            {
                builders.Add(new(letter, IsMark(letter, typical)));
            }
            else
            {
                CollectionsMarshal.AsSpan(builders)[best].Add(letter, IsMark(letter, typical));
            }
        }

        foreach (var builder in builders)
        {
            if (builder.HasLetter)
            {
                lines.Add(builder.ToLine());
            }
        }

        lines.Sort(static (a, b) => a.Top != b.Top ? a.Top.CompareTo(b.Top) : a.Left.CompareTo(b.Left));
    }

    /// <summary>Finds the typical height of a letter: the middle of the blob heights.</summary>
    /// <param name="components">The blobs.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The height, or 0 when there are no blobs tall enough to be letters.</returns>
    public static int TypicalHeight(List<ComponentBox> components, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(scratch);
        var heights = scratch.Heights;
        heights.Clear();
        foreach (var component in components)
        {
            if (component.Height >= MinLetterHeight)
            {
                heights.Add(component.Height);
            }
        }

        if (heights.Count == 0)
        {
            return 0;
        }

        heights.Sort();
        return heights[heights.Count >> 1];
    }

    /// <summary>Determines whether a blob is the size of a letter or mark rather than a picture or a rule.</summary>
    /// <param name="component">The blob.</param>
    /// <param name="typical">The typical letter height.</param>
    /// <returns><see langword="true"/> when it could be part of text.</returns>
    private static bool IsLetterSized(ComponentBox component, int typical)
    {
        if (component.Height > PictureHeights * typical)
        {
            return false;
        }

        return component.Width < RuleWidthHeights * typical || component.Height >= RuleThinShare * typical;
    }

    /// <summary>Determines whether a blob is only a mark, too short to be a letter.</summary>
    /// <param name="component">The blob.</param>
    /// <param name="typical">The typical letter height.</param>
    /// <returns><see langword="true"/> when it is short.</returns>
    private static bool IsMark(ComponentBox component, int typical) => component.Height < MarkShare * typical;

    /// <summary>Finds the line a blob belongs to: the nearest one it sits level with.</summary>
    /// <param name="builders">The lines so far.</param>
    /// <param name="letter">The blob.</param>
    /// <param name="gap">The farthest the blob may stand from a line, in pixels.</param>
    /// <returns>The place of the line, or -1 when none fits.</returns>
    private static int BestLine(List<LineBuilder> builders, ComponentBox letter, double gap)
    {
        var best = -1;
        var bestOverlap = 0;
        for (var i = 0; i < builders.Count; i++)
        {
            var builder = builders[i];
            if (letter.Left - builder.Right > gap)
            {
                continue;
            }

            var overlap = Math.Min(letter.Bottom, builder.Bottom) - Math.Max(letter.Top, builder.Top) + 1;
            if (overlap < JoinOverlap * letter.Height || overlap <= bestOverlap)
            {
                continue;
            }

            best = i;
            bestOverlap = overlap;
        }

        return best;
    }

    /// <summary>A line being built from blobs.</summary>
    [DebuggerDisplay("LineBuilder: ({_left},{Top})-({Right},{Bottom})")]
    internal record struct LineBuilder
    {
        /// <summary>The left edge.</summary>
        private int _left;

        /// <summary>Initializes a new instance of the <see cref="LineBuilder"/> struct.</summary>
        /// <param name="first">The first blob.</param>
        /// <param name="mark">Whether the blob is only a mark.</param>
        internal LineBuilder(ComponentBox first, bool mark)
        {
            _left = first.Left;
            Top = first.Top;
            Right = first.Right;
            Bottom = first.Bottom;
            HasLetter = !mark;
        }

        /// <summary>Gets the right edge.</summary>
        internal int Right { get; private set; }

        /// <summary>Gets the top edge.</summary>
        internal int Top { get; private set; }

        /// <summary>Gets the bottom edge.</summary>
        internal int Bottom { get; private set; }

        /// <summary>Gets a value indicating whether any blob is tall enough to be a letter.</summary>
        internal bool HasLetter { get; private set; }

        /// <summary>Adds a blob to the line.</summary>
        /// <param name="component">The blob.</param>
        /// <param name="mark">Whether the blob is only a mark.</param>
        internal void Add(ComponentBox component, bool mark)
        {
            _left = Math.Min(_left, component.Left);
            Top = Math.Min(Top, component.Top);
            Right = Math.Max(Right, component.Right);
            Bottom = Math.Max(Bottom, component.Bottom);
            HasLetter |= !mark;
        }

        /// <summary>Gets the line's box.</summary>
        /// <returns>The box.</returns>
        internal readonly TextLine ToLine() => new(_left, Top, Right, Bottom);
    }
}
