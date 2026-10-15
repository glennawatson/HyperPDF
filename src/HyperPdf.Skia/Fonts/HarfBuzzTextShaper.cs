// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Media.TextFormatting.Unicode;
using Avalonia.Platform;
using HarfBuzzSharp;
using ShapingBuffer = HarfBuzzSharp.Buffer;

namespace HyperPdf.Skia.Fonts;

/// <summary>Shapes text with cached OpenType fonts and a reusable thread buffer.</summary>
internal sealed class HarfBuzzTextShaper : ITextShaperImpl, IDisposable
{
    /// <summary>The default tab width in spaces.</summary>
    private const int TabSpaces = 4;

    /// <summary>The registered shaper's working buffers, separated by rendering thread.</summary>
    private readonly ConditionalWeakTable<Thread, ShapingThreadState> _threads = new();

    /// <summary>One after the host releases the text shaper.</summary>
    private int _disposed;

    /// <inheritdoc />
    public ITextShaperTypeface CreateTypeface(GlyphTypeface glyphTypeface)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(glyphTypeface);
        return new ShapingTypeface((SkiaTypeface)glyphTypeface.PlatformTypeface);
    }

    /// <inheritdoc />
    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (text.IsEmpty)
        {
            return new(text, 0, options.GlyphTypeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        var typeface = (ShapingTypeface)options.GlyphTypeface.TextShaperTypeface;
        var state = _threads.GetValue(Thread.CurrentThread, static _ => new());
        var buffer = state.Buffer;
        buffer.Reset();
        var containing = GetContainingText(text, out var start);
        buffer.AddUtf16(containing.Span, start, text.Length);
        MergeBreakPair(buffer);
        buffer.GuessSegmentProperties();
        buffer.Direction = (options.BidiLevel & 1) == 0 ? Direction.LeftToRight : Direction.RightToLeft;
        buffer.Language = state.GetLanguage(options.Culture ?? CultureInfo.CurrentCulture);
        typeface.Font.Shape(buffer, GetFeatures(state, options.FontFeatures));
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var scale = options.FontRenderingEmSize / typeface.UnitsPerEm;
        var result = new ShapedBuffer(text, infos.Length, options.GlyphTypeface, options.FontRenderingEmSize, options.BidiLevel);
        for (var i = 0; i < infos.Length; i++)
        {
            var cluster = (int)infos[i].Cluster - start;
            var glyph = (ushort)infos[i].Codepoint;
            var advance = (positions[i].XAdvance * scale) + options.LetterSpacing;
            if ((uint)cluster < (uint)text.Length && text.Span[cluster] == '\t')
            {
                glyph = options.GlyphTypeface.CharacterToGlyphMap[' '];
                _ = options.GlyphTypeface.TryGetHorizontalGlyphAdvance(glyph, out var spaceAdvance);
                advance = options.IncrementalTabWidth > 0 ? options.IncrementalTabWidth : TabSpaces * spaceAdvance * scale;
            }

            var offset = new Vector(positions[i].XOffset * scale, -positions[i].YOffset * scale);
            result[i] = new(glyph, cluster, advance, offset);
        }

        return result;
    }

    /// <summary>Releases shaping resources after the host stops text layout.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var (_, state) in _threads)
        {
            state.Dispose();
        }

        _threads.Clear();
    }

    /// <summary>Includes surrounding text when shaping a slice.</summary>
    /// <param name="buffer">The Unicode buffer before shaping.</param>
    private static unsafe void MergeBreakPair(ShapingBuffer buffer)
    {
        var infos = buffer.GetGlyphInfoSpan();
        var last = infos.Length - 1;
        if (last < 0 || !new Codepoint(infos[last].Codepoint).IsBreakChar)
        {
            return;
        }

        fixed (HarfBuzzSharp.GlyphInfo* glyphs = infos)
        {
            if (last > 0 && glyphs[last - 1].Codepoint == '\r' && glyphs[last].Codepoint == '\n')
            {
                glyphs[last - 1].Codepoint = '\u200C';
                glyphs[last].Cluster = glyphs[last - 1].Cluster;
            }

            glyphs[last].Codepoint = '\u200C';
        }
    }

    /// <summary>Includes surrounding text when shaping a slice.</summary>
    /// <param name="text">The text slice.</param>
    /// <param name="start">The slice offset.</param>
    /// <returns>The containing text.</returns>
    private static ReadOnlyMemory<char> GetContainingText(ReadOnlyMemory<char> text, out int start)
    {
        if (MemoryMarshal.TryGetString(text, out var value, out start, out _))
        {
            return value.AsMemory();
        }

        if (MemoryMarshal.TryGetArray(text, out var segment))
        {
            start = segment.Offset;
            return segment.Array.AsMemory();
        }

        start = 0;
        return text;
    }

    /// <summary>Converts requested font features for HarfBuzz.</summary>
    /// <param name="state">The registered shaper's thread-owned conversion storage.</param>
    /// <param name="features">The OpenType feature overrides.</param>
    /// <returns>The shaping features.</returns>
    private static Feature[] GetFeatures(ShapingThreadState state, IReadOnlyList<FontFeature>? features)
    {
        if (features is null || features.Count == 0)
        {
            return [];
        }

        var result = state.GetFeatures(features.Count);

        for (var i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            result[i] = new(Tag.Parse(feature.Tag), (uint)feature.Value, (uint)feature.Start, (uint)feature.End);
        }

        return result;
    }
}
