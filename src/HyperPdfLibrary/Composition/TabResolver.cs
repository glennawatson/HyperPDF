// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Works out how wide a tab is: the distance to the next tab stop, less what the stop's alignment takes.</summary>
internal static class TabResolver
{
    /// <summary>The distance in points between the default tab stops that follow the last stop of the paragraph.</summary>
    private const float DefaultInterval = 36F;

    /// <summary>The smallest distance a tab may move, so a tab at a stop jumps to the next one.</summary>
    private const float MinimumGap = 0.5F;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>Works out the width of the tab at an atom.</summary>
    /// <param name="atoms">The atoms of the paragraph.</param>
    /// <param name="tabIndex">The tab's place in <paramref name="atoms"/>.</param>
    /// <param name="stops">The paragraph's tab stops, nearest the left edge first.</param>
    /// <param name="position">Where the tab starts, from the left edge of the text area.</param>
    /// <returns>The width and what fills it.</returns>
    internal static TabGap Resolve(List<Atom> atoms, int tabIndex, PdfTabStop[] stops, float position)
    {
        var stop = FindStop(stops, position);
        var after = MeasureAfter(atoms, tabIndex + 1, stop.Alignment);
        var gap = stop.Alignment switch
        {
            PdfTabAlignment.Right or PdfTabAlignment.Decimal => stop.Position - position - after,
            PdfTabAlignment.Center => stop.Position - position - (after * Half),
            _ => stop.Position - position,
        };
        return new(tabIndex, MathF.Max(gap, 0F), stop.Leader);
    }

    /// <summary>Finds the decimal point of a number: the last full stop, or the last comma when there is no full stop, so <c>1,234.50</c> lines up on its full stop.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The index of the decimal point, or -1 when there is none.</returns>
    private static int FindDecimalPoint(string text)
    {
        var point = text.LastIndexOf('.');
        return point >= 0 ? point : text.LastIndexOf(',');
    }

    /// <summary>Finds the first stop past a position, falling back to the default stops.</summary>
    /// <param name="stops">The paragraph's stops.</param>
    /// <param name="position">The position.</param>
    /// <returns>The stop.</returns>
    private static PdfTabStop FindStop(PdfTabStop[] stops, float position)
    {
        foreach (var stop in stops)
        {
            if (stop.Position > position + MinimumGap)
            {
                return stop;
            }
        }

        var next = (MathF.Floor(position / DefaultInterval) + 1F) * DefaultInterval;
        return PdfTabStop.At(next > position + MinimumGap ? next : next + DefaultInterval);
    }

    /// <summary>Measures the text after a tab up to the next tab or break: all of it, or for a decimal tab the part before the decimal point.</summary>
    /// <param name="atoms">The atoms of the paragraph.</param>
    /// <param name="from">The first atom after the tab.</param>
    /// <param name="alignment">The alignment of the stop.</param>
    /// <returns>The width in points.</returns>
    private static float MeasureAfter(List<Atom> atoms, int from, PdfTabAlignment alignment)
    {
        if (alignment == PdfTabAlignment.Left)
        {
            return 0F;
        }

        var width = 0F;
        for (var i = from; i < atoms.Count && atoms[i].Kind is not (AtomKind.Tab or AtomKind.Break); i++)
        {
            var atom = atoms[i];
            if (alignment == PdfTabAlignment.Decimal && atom.Kind == AtomKind.Word && FindDecimalPoint(atom.Text) is var point and >= 0)
            {
                return width + atom.Font!.Measure(atom.Text.AsSpan(0, point), atom.Size);
            }

            width += atom.Width;
        }

        return width;
    }
}
