// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The characters of one rich text paragraph as a single row of units, so lines can break across runs. Each unit knows the
/// run it came from, the place in that run's codes and how wide it is drawn.
/// </summary>
[DebuggerDisplay("RichFlow: {Length} units")]
internal sealed class RichFlow
{
    /// <summary>One thousand: glyph widths are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The codes of each run.</summary>
    private readonly FormCodes[] _codes;

    /// <summary>The run each unit came from.</summary>
    private readonly int[] _runOf;

    /// <summary>The place of each unit in its run's codes.</summary>
    private readonly int[] _indexOf;

    /// <summary>The width of each unit in points.</summary>
    private readonly float[] _widths;

    /// <summary>Initializes a new instance of the <see cref="RichFlow"/> class.</summary>
    /// <param name="codes">The encoded text of each run.</param>
    /// <param name="sizes">The font size of each run.</param>
    internal RichFlow(FormCodes[] codes, float[] sizes)
    {
        _codes = codes;
        var total = 0;
        foreach (var run in codes)
        {
            total += run.Length;
        }

        _runOf = new int[total];
        _indexOf = new int[total];
        _widths = new float[total];
        var unit = 0;
        for (var r = 0; r < codes.Length; r++)
        {
            for (var i = 0; i < codes[r].Length; i++)
            {
                _runOf[unit] = r;
                _indexOf[unit] = i;
                _widths[unit] = codes[r].GetWidth(i) * sizes[r] / GlyphUnits;
                unit++;
            }
        }
    }

    /// <summary>Gets the number of units.</summary>
    internal int Length => _widths.Length;

    /// <summary>Gets the run a unit came from.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The run's index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int RunOf(int unit) => _runOf[unit];

    /// <summary>Gets the place of a unit in its run's codes.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int IndexOf(int unit) => _indexOf[unit];

    /// <summary>Gets the encoded bytes of some units of one run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="first">The first unit's place in the run.</param>
    /// <param name="count">The number of units.</param>
    /// <returns>The bytes to show.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Slice(int run, int first, int count) => _codes[run].Slice(first, count);

    /// <summary>Measures a range of units.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The width in points.</returns>
    internal float Measure(int start, int length)
    {
        var total = 0F;
        for (var i = start; i < start + length; i++)
        {
            total += _widths[i];
        }

        return total;
    }

    /// <summary>Gets the width of a range of units without its trailing spaces.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The width in points.</returns>
    internal float MeasureTrimmed(int start, int length)
    {
        var end = start + length;
        while (end > start && _codes[_runOf[end - 1]].GetKind(_indexOf[end - 1]) == FormCodes.Space)
        {
            end--;
        }

        return Measure(start, end - start);
    }

    /// <summary>Splits the units at line feeds and wraps lines wider than the space allows, at spaces where possible.</summary>
    /// <param name="maxWidth">The widest a line may be, in points.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="cancellationToken">A token that cancels the layout.</param>
    internal void Wrap(float maxWidth, List<TextLine> lines, CancellationToken cancellationToken)
    {
        var start = 0;
        var width = 0F;
        var breakAt = -1;
        for (var i = 0; i < Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = _codes[_runOf[i]].GetKind(_indexOf[i]);
            if (kind == FormCodes.LineFeed)
            {
                AddLine(start, i, lines);
                start = i + 1;
                width = 0;
                breakAt = -1;
                continue;
            }

            if (width + _widths[i] > maxWidth && i > start)
            {
                start = Break(start, i, breakAt, lines);
                width = Measure(start, i - start);
                breakAt = -1;
            }

            width += _widths[i];
            breakAt = kind == FormCodes.Space ? i : breakAt;
        }

        AddLine(start, Length, lines);
    }

    /// <summary>Ends a line before the unit that no longer fits.</summary>
    /// <param name="start">The line's first unit.</param>
    /// <param name="current">The unit that does not fit.</param>
    /// <param name="breakAt">The last space on the line, or -1.</param>
    /// <param name="lines">Receives the line.</param>
    /// <returns>The first unit of the next line.</returns>
    private int Break(int start, int current, int breakAt, List<TextLine> lines)
    {
        if (breakAt > start)
        {
            AddLine(start, breakAt, lines);
            return breakAt + 1;
        }

        AddLine(start, current, lines);
        return current;
    }

    /// <summary>Adds a line.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="end">One past the last unit.</param>
    /// <param name="lines">Receives the line.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddLine(int start, int end, List<TextLine> lines) => lines.Add(new(start, end - start, MeasureTrimmed(start, end - start)));
}
