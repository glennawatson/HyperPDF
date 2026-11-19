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

    /// <summary>The bundled family the serif text is set in.</summary>
    private const string SerifFamily = "Tinos";

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

    /// <summary>The widths of whole pieces of text measured so far, which a layout asks for again each time it is built.</summary>
    private readonly Dictionary<TextRunKey, int> _widths = [with(Capacity)];

    /// <summary>The text cut short to fit a width so far.</summary>
    private readonly Dictionary<TextFitKey, string> _fits = [];

    /// <summary>The coverage buffer every piece of text is drawn through.</summary>
    private readonly CoverageBuffer _coverage = new();

    /// <summary>The layout of text the bundled font cannot set alone, with the system fonts it falls back to.</summary>
    private readonly TextLayout _layout = new();

    /// <summary>The surfaces of the render backend.</summary>
    private readonly IRenderSurfaceFactory _surfaces;

    /// <summary>The regular face, or <see langword="null"/> when the bundled font could not be read.</summary>
    private readonly UiFont? _regular = UiFont.Load(false);

    /// <summary>The bold face.</summary>
    private readonly UiFont? _bold = UiFont.Load(true);

    /// <summary>The regular serif face, read when serif text is first asked for.</summary>
    private UiFont? _serifRegular;

    /// <summary>The bold serif face, read when serif text is first asked for.</summary>
    private UiFont? _serifBold;

    /// <summary>Whether the serif faces have been read, whether or not they could be.</summary>
    private bool _serifLoaded;

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
    public int Measure(ReadOnlySpan<char> text, TextStyle style)
    {
        if (FaceOf(style) is not { } face)
        {
            return 0;
        }

        // Text the bundled font has every character of needs no shaping and no other font, which is nearly all window text.
        return (int)MathF.Ceiling(face.HasAll(text) ? face.Measure(text, style.Size) : _layout.Run(text, face, style.Size));
    }

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

    /// <summary>Measures a whole piece of text, keeping the width so the next layout that shows the same text does not measure it again.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <returns>The width in device pixels.</returns>
    public int Width(string text, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        var key = new TextRunKey(text, style);
        if (_widths.TryGetValue(key, out var width))
        {
            return width;
        }

        // A face that could not be read measures nothing, and may be read later.
        if (FaceOf(style) is null)
        {
            return 0;
        }

        if (_widths.Count >= Capacity)
        {
            _widths.Clear();
        }

        width = Measure(text, style);
        _widths.Add(key, width);
        return width;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Clear();

    /// <summary>Writes one piece of drawn coverage into a surface as premultiplied pixels of one colour.</summary>
    /// <param name="target">The surface pixels.</param>
    /// <param name="ink">The coverage and the colour.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    internal static bool WriteInk(RenderTarget target, in TextInk ink)
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

    /// <summary>Finds text that was cut short to fit a width before.</summary>
    /// <param name="text">The whole text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it must fit, in device pixels.</param>
    /// <returns>The cut text, or <see langword="null"/> when this text has not been cut to this width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string? FindFit(string text, TextStyle style, int width) => _fits.GetValueOrDefault(new(text, style, width));

    /// <summary>Keeps text cut short to fit a width, so the next layout does not cut it again.</summary>
    /// <param name="text">The whole text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it fits.</param>
    /// <param name="fitted">The text as cut.</param>
    /// <returns>The text kept.</returns>
    internal string KeepFit(string text, TextStyle style, int width, string fitted)
    {
        if (_fits.Count >= Capacity)
        {
            _fits.Clear();
        }

        _fits[new(text, style, width)] = fitted;
        return fitted;
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

        var plain = face.HasAll(text);
        var measured = plain ? face.Measure(text, style.Size) : _layout.Run(text, face, style.Size);
        var ascent = face.Ascent(style.Size);
        var descent = face.Descent(style.Size);
        if (!plain)
        {
            // A system font for another script can stand taller or hang lower than the bundled one, so the surface makes room for it.
            foreach (var glyph in _layout.Placed)
            {
                ascent = MathF.Max(ascent, glyph.Font.Ascent(style.Size) + glyph.Rise);
                descent = MathF.Max(descent, glyph.Font.Descent(style.Size) - glyph.Rise);
            }
        }

        var width = (int)MathF.Ceiling(measured) + Padding + Padding;
        var height = (int)MathF.Ceiling(ascent + descent) + Padding + Padding;
        _coverage.Reset(width, height);
        var baseline = Padding + ascent;
        if (plain)
        {
            face.Draw(text, style.Size, new(Padding, baseline), _coverage);
        }
        else
        {
            foreach (var glyph in _layout.Placed)
            {
                glyph.Font.DrawGlyph(glyph.Glyph, style.Size, new(Padding + glyph.X, baseline - glyph.Rise), _coverage);
            }
        }

        var surface = _surfaces.Create(width, height);
        _ = surface.Write(new TextInk(_coverage, style.Color), WriteInk);
        return new(surface, width, height);
    }

    /// <summary>Picks the face of a style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font could not be read.</returns>
    private UiFont? FaceOf(TextStyle style)
    {
        if (!style.Serif)
        {
            return style.Bold ? _bold ?? _regular : _regular;
        }

        // The serif faces are read the first time text asks for them, which only focus mode does.
        if (!_serifLoaded)
        {
            _serifRegular = UiFont.Load(SerifFamily, false);
            _serifBold = UiFont.Load(SerifFamily, true);
            _serifLoaded = true;
        }

        var serif = style.Bold ? _serifBold ?? _serifRegular : _serifRegular;
        return serif ?? (style.Bold ? _bold ?? _regular : _regular);
    }

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
