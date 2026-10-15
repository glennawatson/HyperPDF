// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// A rich text paragraph laid out: the font, size and codes of each run, and every character as one row of units so lines
/// can break across runs. Each unit knows the run it came from, its place in that run's codes and how wide it is drawn. An
/// instance is rented, filled, used and disposed, and its arrays are reused by the next paragraph.
/// </summary>
[DebuggerDisplay("RichBlock: {LineCount} lines, {Length} units")]
internal sealed class RichBlock : IDisposable
{
    /// <summary>One thousand: glyph widths are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The factor by which a full array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The number of lines room is first made for.</summary>
    private const int InitialLines = 4;

    /// <summary>The most units an idle instance keeps room for; a larger one is left to the collector.</summary>
    private const int MaxKeptUnits = 16_384;

    /// <summary>The document-owned pool this instance returns to.</summary>
    private RichBlock?[]? _pool;

    /// <summary>The font of each run.</summary>
    private FormFont[] _fonts = [];

    /// <summary>The size of each run.</summary>
    private float[] _sizes = [];

    /// <summary>The style of each run.</summary>
    private RichStyle[] _styles = [];

    /// <summary>The codes of each run.</summary>
    private FormCodes?[] _codes = [];

    /// <summary>The run each unit came from.</summary>
    private int[] _runOf = [];

    /// <summary>The place of each unit in its run's codes.</summary>
    private int[] _indexOf = [];

    /// <summary>The width of each unit in points.</summary>
    private float[] _widths = [];

    /// <summary>The lines, as ranges of units.</summary>
    private TextLine[] _lines = [];

    /// <summary>The number of runs.</summary>
    private int _runs;

    /// <summary>Whether the instance is held by a caller, so a second dispose is ignored.</summary>
    private bool _rented;

    /// <summary>Gets the number of units.</summary>
    internal int Length { get; private set; }

    /// <summary>Gets the number of lines.</summary>
    internal int LineCount { get; private set; }

    /// <summary>Gets the number of runs.</summary>
    internal int RunCount => _runs;

    /// <summary>Gives the instance and its codes back for the next paragraph.</summary>
    public void Dispose()
    {
        if (!_rented)
        {
            return;
        }

        _rented = false;
        for (var r = 0; r < _runs; r++)
        {
            ReleaseCodes(r);
            _fonts[r] = null!;
        }

        if (_widths.Length > MaxKeptUnits || _pool is not { } pool)
        {
            return;
        }

        for (var i = 0; i < pool.Length; i++)
        {
            if (Interlocked.CompareExchange(ref pool[i], this, null) is null)
            {
                return;
            }
        }
    }

    /// <summary>Gets an instance to fill.</summary>
    /// <param name="runs">The number of runs the paragraph has.</param>
    /// <param name="pool">The owning document's idle instances, or null for an uncached operation.</param>
    /// <returns>The instance; dispose it to give its arrays back.</returns>
    internal static RichBlock Rent(int runs, RichBlock?[]? pool)
    {
        RichBlock? block = null;
        for (var i = 0; pool is not null && i < pool.Length && block is null; i++)
        {
            block = Interlocked.Exchange(ref pool[i], null);
        }

        block ??= new();
        block._pool = pool;
        block._rented = true;
        block._runs = runs;
        block.LineCount = 0;
        block.Length = 0;
        Ensure(ref block._fonts, runs);
        Ensure(ref block._sizes, runs);
        Ensure(ref block._styles, runs);
        Ensure(ref block._codes, runs);
        return block;
    }

    /// <summary>Gets the font of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FormFont GetFont(int run) => _fonts[run];

    /// <summary>Gets the size of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The size in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float GetSize(int run) => _sizes[run];

    /// <summary>Gets the style of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RichStyle GetStyle(int run) => _styles[run];

    /// <summary>Gets a line.</summary>
    /// <param name="index">The line's place.</param>
    /// <returns>The line, as a range of units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TextLine GetLine(int index) => _lines[index];

    /// <summary>Sets up a run and encodes its text.</summary>
    /// <param name="run">The run's place.</param>
    /// <param name="font">The run's font.</param>
    /// <param name="size">The run's size.</param>
    /// <param name="style">The run's style.</param>
    /// <param name="text">The run's text.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    internal void SetRun(int run, FormFont font, float size, RichStyle style, ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        _fonts[run] = font;
        _sizes[run] = size;
        _styles[run] = style;
        ReleaseCodes(run);
        _codes[run] = font.Encode(text, cancellationToken);
    }

    /// <summary>Lays the units out: works out each unit's run, place and width.</summary>
    internal void Flow()
    {
        var total = 0;
        for (var r = 0; r < _runs; r++)
        {
            total += _codes[r]!.Length;
        }

        Length = total;
        Ensure(ref _runOf, total);
        Ensure(ref _indexOf, total);
        Ensure(ref _widths, total);
        var unit = 0;
        for (var r = 0; r < _runs; r++)
        {
            var codes = _codes[r]!;
            for (var i = 0; i < codes.Length; i++)
            {
                _runOf[unit] = r;
                _indexOf[unit] = i;
                _widths[unit] = codes.GetWidth(i) * _sizes[r] / GlyphUnits;
                unit++;
            }
        }
    }

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
    internal ReadOnlySpan<byte> Slice(int run, int first, int count) => _codes[run]!.Slice(first, count);

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

    /// <summary>Splits the units at line feeds and wraps lines wider than the space allows, at spaces where possible.</summary>
    /// <param name="maxWidth">The widest a line may be, in points.</param>
    /// <param name="cancellationToken">A token that cancels the layout.</param>
    internal void Wrap(float maxWidth, CancellationToken cancellationToken)
    {
        var start = 0;
        var width = 0F;
        var breakAt = -1;
        for (var i = 0; i < Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = _codes[_runOf[i]]!.GetKind(_indexOf[i]);
            if (kind == FormCodes.LineFeed)
            {
                AddLine(start, i);
                start = i + 1;
                width = 0;
                breakAt = -1;
                continue;
            }

            if (width + _widths[i] > maxWidth && i > start)
            {
                start = Break(start, i, breakAt);
                width = Measure(start, i - start);
                breakAt = -1;
            }

            width += _widths[i];
            breakAt = kind == FormCodes.Space ? i : breakAt;
        }

        AddLine(start, Length);
    }

    /// <summary>Makes an array at least a size, keeping nothing of the old contents.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="size">The elements needed.</param>
    private static void Ensure<T>(ref T[] array, int size)
    {
        if (array.Length < size)
        {
            array = new T[Math.Max(size, array.Length * GrowthFactor)];
        }
    }

    /// <summary>Releases a run's encoded units before replacing or returning its layout.</summary>
    /// <param name="run">The run.</param>
    private void ReleaseCodes(int run)
    {
        var previous = _codes[run];
        _codes[run] = null;
        previous?.Dispose();
    }

    /// <summary>Gets the width of a range of units without its trailing spaces.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <returns>The width in points.</returns>
    private float MeasureTrimmed(int start, int length)
    {
        var end = start + length;
        while (end > start && _codes[_runOf[end - 1]]!.GetKind(_indexOf[end - 1]) == FormCodes.Space)
        {
            end--;
        }

        return Measure(start, end - start);
    }

    /// <summary>Ends a line before the unit that no longer fits.</summary>
    /// <param name="start">The line's first unit.</param>
    /// <param name="current">The unit that does not fit.</param>
    /// <param name="breakAt">The last space on the line, or -1.</param>
    /// <returns>The first unit of the next line.</returns>
    private int Break(int start, int current, int breakAt)
    {
        if (breakAt > start)
        {
            AddLine(start, breakAt);
            return breakAt + 1;
        }

        AddLine(start, current);
        return current;
    }

    /// <summary>Adds a line.</summary>
    /// <param name="start">The first unit.</param>
    /// <param name="end">One past the last unit.</param>
    private void AddLine(int start, int end)
    {
        if (LineCount == _lines.Length)
        {
            Array.Resize(ref _lines, Math.Max(LineCount * GrowthFactor, InitialLines));
        }

        _lines[LineCount] = new(start, end - start, MeasureTrimmed(start, end - start));
        LineCount++;
    }
}
