// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Writes a shaped run into a content stream: glyph codes in <c>TJ</c> arrays whose adjustments turn the font's widths
/// into the shaped advances and offsets, wrapped in <c>/ActualText</c> marked content where a group needs it.
/// </summary>
internal static class RunWriter
{
    /// <summary>Thousandths in one em, the unit of text adjustments.</summary>
    private const float Thousand = 1000F;

    /// <summary>The bytes of a glyph code.</summary>
    private const int CodeBytes = 2;

    /// <summary>Writes the groups of a run.</summary>
    /// <param name="builder">The content stream.</param>
    /// <param name="context">The font the run is shown in.</param>
    /// <param name="text">The text in logical order.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <param name="groups">The groups the run is divided into.</param>
    /// <param name="fontSize">The font size in text space units, which scales the text rise.</param>
    internal static void Write(ref PdfContentBuilder builder, in RunContext context, ReadOnlySpan<char> text, ReadOnlySpan<PdfShapedGlyph> glyphs, List<ClusterGroup> groups, float fontSize)
    {
        foreach (var group in groups)
        {
            if (group.Mark)
            {
                BeginActualText(ref builder, context.Sink, text.Slice(group.TextStart, group.TextLength));
            }

            WriteGroup(ref builder, context, glyphs.Slice(group.FirstGlyph, group.GlyphCount), fontSize);
            if (group.Mark)
            {
                builder.EndMarkedContent();
            }
        }
    }

    /// <summary>Starts a <c>Span</c> with the text the glyphs stand for.</summary>
    /// <param name="builder">The content stream.</param>
    /// <param name="sink">The document.</param>
    /// <param name="text">The text.</param>
    private static void BeginActualText(ref PdfContentBuilder builder, IPdfObjectSink sink, ReadOnlySpan<char> text)
    {
        var properties = new PdfDictionary(sink.Owner, 1);
        properties.Set(KnownName.ActualText, PdfValue.FromString(PdfText.Encode(text.ToString())));
        builder.BeginMarkedContent("Span"u8, properties, sink.Names);
    }

    /// <summary>Writes some glyphs.</summary>
    /// <param name="builder">The content stream.</param>
    /// <param name="context">The font.</param>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="fontSize">The font size.</param>
    private static void WriteGroup(ref PdfContentBuilder builder, in RunContext context, ReadOnlySpan<PdfShapedGlyph> glyphs, float fontSize)
    {
        TextArray array = new(glyphs.Length * CodeBytes);
        try
        {
            foreach (var glyph in glyphs)
            {
                var code = context.Use.GetCode(glyph.Glyph);
                var width = context.Width(code == 0 ? 0 : glyph.Glyph);
                if (context.Mode == PdfWritingMode.Vertical)
                {
                    // Vertical glyph displacement is negative, and a TJ number is subtracted from y, so the number is w1 + advance.
                    array.Add(ref builder, code);
                    array.Pending += width + (glyph.Advance * Thousand);
                    continue;
                }

                array.Pending -= glyph.OffsetX * Thousand;
                if (glyph.OffsetY != 0)
                {
                    array.Close(ref builder);
                    builder.SetTextRise(glyph.OffsetY * fontSize);
                    array.Add(ref builder, code);
                    array.Pending += width - (glyph.Advance * Thousand) + (glyph.OffsetX * Thousand);
                    array.Close(ref builder);
                    builder.SetTextRise(0);
                    continue;
                }

                array.Add(ref builder, code);
                array.Pending += width - (glyph.Advance * Thousand) + (glyph.OffsetX * Thousand);
            }

            array.Close(ref builder);
        }
        finally
        {
            array.Dispose();
        }
    }

    /// <summary>A <c>TJ</c> array being written: the string of codes so far and the adjustment waiting to be written before the next one.</summary>
    private ref struct TextArray : IDisposable
    {
        /// <summary>The smallest adjustment worth writing, in thousandths of an em.</summary>
        private const float Negligible = 0.005F;

        /// <summary>The bits in a byte.</summary>
        private const int ByteBits = 8;

        /// <summary>The codes not yet written as a string.</summary>
        private readonly byte[] _codes;

        /// <summary>The number of bytes of <see cref="_codes"/> in use.</summary>
        private int _length;

        /// <summary>Whether the array is open.</summary>
        private bool _open;

        /// <summary>Initializes a new instance of the <see cref="TextArray"/> struct.</summary>
        /// <param name="capacity">The room for codes to start with.</param>
        internal TextArray(int capacity)
        {
            _codes = ArrayPool<byte>.Shared.Rent(capacity);
            _length = 0;
            _open = false;
            Pending = 0;
        }

        /// <summary>Gets or sets the adjustment, in thousandths of an em, that moves the pen left, waiting to be written.</summary>
        internal float Pending { get; set; }

        /// <summary>Releases the buffer.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose() => ArrayPool<byte>.Shared.Return(_codes);

        /// <summary>Adds a glyph code, first writing any adjustment waiting.</summary>
        /// <param name="builder">The content stream.</param>
        /// <param name="code">The code.</param>
        internal void Add(ref PdfContentBuilder builder, int code)
        {
            if (!_open)
            {
                builder.BeginTextArray();
                _open = true;
            }

            if (MathF.Abs(Pending) >= Negligible)
            {
                Flush(ref builder);
                builder.AddTextArrayAdjustment(Pending);
            }

            Pending = 0;
            _codes[_length] = (byte)(code >> ByteBits);
            _codes[_length + 1] = (byte)code;
            _length += CodeBytes;
        }

        /// <summary>Ends the array, writing the codes and any adjustment waiting.</summary>
        /// <param name="builder">The content stream.</param>
        internal void Close(ref PdfContentBuilder builder)
        {
            var moves = MathF.Abs(Pending) >= Negligible;
            if (!_open && !moves)
            {
                return;
            }

            if (!_open)
            {
                builder.BeginTextArray();
            }

            Flush(ref builder);
            if (moves)
            {
                builder.AddTextArrayAdjustment(Pending);
            }

            builder.EndTextArray();
            _open = false;
            Pending = 0;
        }

        /// <summary>Writes the codes so far as one string.</summary>
        /// <param name="builder">The content stream.</param>
        private void Flush(ref PdfContentBuilder builder)
        {
            if (_length == 0)
            {
                return;
            }

            builder.AddTextArrayString(_codes.AsSpan(0, _length));
            _length = 0;
        }
    }
}
