// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Text;

/// <summary>
/// Draws the window's own text. Each piece of text is drawn once into a surface of the render backend and kept, so a frame only
/// places surfaces it already has. Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("TextRuns: {_runs.Count} kept")]
public sealed class TextRuns : IDisposable
{
    /// <summary>The pieces of text kept before all are dropped. Window text is a small, steady set, so this is rarely reached.</summary>
    private const int Capacity = 768;

    /// <summary>The empty pixels kept around the text, so smoothed edges are not cut off.</summary>
    private const int Padding = 1;

    /// <summary>The shift of the alpha channel in a colour.</summary>
    private const int AlphaShift = 24;

    /// <summary>The shift of the red channel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green channel.</summary>
    private const int GreenShift = 8;

    /// <summary>The largest value of a channel.</summary>
    private const uint ChannelMax = 255;

    /// <summary>The run of text with nothing to draw.</summary>
    private static readonly TextRun Empty = new(null, 0, 0);

    /// <summary>The pieces of text drawn so far.</summary>
    private readonly Dictionary<TextRunKey, TextRun> _runs = [with(Capacity)];

    /// <summary>The coverage buffer every piece of text is drawn through.</summary>
    private readonly CoverageBuffer _coverage = new();

    /// <summary>The surfaces of the render backend.</summary>
    private readonly IRenderSurfaceFactory _surfaces;

    /// <summary>The regular face, or <see langword="null"/> when the bundled font could not be read.</summary>
    private readonly UiFont? _regular = UiFont.Load(false);

    /// <summary>The bold face.</summary>
    private readonly UiFont? _bold = UiFont.Load(true);

    /// <summary>Initializes a new instance of the <see cref="TextRuns"/> class.</summary>
    /// <param name="surfaces">The surfaces of the render backend that draws the window.</param>
    public TextRuns(IRenderSurfaceFactory surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        _surfaces = surfaces;
    }

    /// <summary>Measures a piece of text without drawing it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <returns>The width in device pixels.</returns>
    public int Measure(ReadOnlySpan<char> text, TextStyle style) =>
        FaceOf(style) is { } face ? (int)MathF.Ceiling(face.Measure(text, style.Size)) : 0;

    /// <summary>Gets the height of a line of text.</summary>
    /// <param name="style">How the text looks.</param>
    /// <returns>The height in device pixels.</returns>
    public int LineHeight(TextStyle style) =>
        FaceOf(style) is { } face ? (int)MathF.Ceiling(face.Ascent(style.Size) + face.Descent(style.Size)) + Padding + Padding : 0;

    /// <summary>Gets a piece of text as a surface, drawing it the first time it is asked for.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <returns>The drawn text.</returns>
    public TextRun Get(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        var key = new TextRunKey(text, style);
        if (_runs.TryGetValue(key, out var run))
        {
            return run;
        }

        if (_runs.Count >= Capacity)
        {
            Clear();
        }

        run = Draw(text, style);
        _runs.Add(key, run);
        return run;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Clear();

    /// <summary>Writes one piece of drawn coverage into a surface as premultiplied pixels of one colour.</summary>
    /// <param name="target">The surface pixels.</param>
    /// <param name="ink">The coverage and the colour.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool WriteInk(RenderTarget target, in TextInk ink)
    {
        var row = ArrayPool<byte>.Shared.Rent(target.Width);
        var alpha = ink.Color >> AlphaShift;
        var red = (ink.Color >> RedShift) & ChannelMax;
        var green = (ink.Color >> GreenShift) & ChannelMax;
        var blue = ink.Color & ChannelMax;
        for (var y = 0; y < target.Height; y++)
        {
            ink.Coverage.ReadRow(y, row);
            var pixels = MemoryMarshal.Cast<byte, uint>(target.Pixels.Slice(y * target.Stride, target.Width * sizeof(uint)));
            for (var x = 0; x < pixels.Length; x++)
            {
                var covered = (uint)PremultipliedColor.DivideBy255((int)(row[x] * alpha));
                pixels[x] = (covered << AlphaShift) | (Scale(red, covered) << RedShift) | (Scale(green, covered) << GreenShift) | Scale(blue, covered);
            }
        }

        ArrayPool<byte>.Shared.Return(row);
        return true;
    }

    /// <summary>Scales a channel by an alpha.</summary>
    /// <param name="channel">The channel, from 0 to 255.</param>
    /// <param name="alpha">The alpha, from 0 to 255.</param>
    /// <returns>The premultiplied channel.</returns>
    private static uint Scale(uint channel, uint alpha) => (uint)PremultipliedColor.DivideBy255((int)(channel * alpha));

    /// <summary>Draws a piece of text into a new surface.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <returns>The drawn text.</returns>
    private TextRun Draw(string text, TextStyle style)
    {
        if (FaceOf(style) is not { } face || text.Length == 0 || style.Size <= 0)
        {
            return Empty;
        }

        var width = (int)MathF.Ceiling(face.Measure(text, style.Size)) + Padding + Padding;
        var height = LineHeight(style);
        _coverage.Reset(width, height);
        face.Draw(text, style.Size, new(Padding, Padding + face.Ascent(style.Size)), _coverage);
        var surface = _surfaces.Create(width, height);
        _ = surface.Write(new TextInk(_coverage, style.Color), WriteInk);
        return new(surface, width, height);
    }

    /// <summary>Picks the face of a style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font could not be read.</returns>
    private UiFont? FaceOf(TextStyle style) => style.Bold ? _bold ?? _regular : _regular;

    /// <summary>Frees every surface kept.</summary>
    private void Clear()
    {
        foreach (var kept in _runs)
        {
            kept.Value.Surface?.Dispose();
        }

        _runs.Clear();
    }
}
