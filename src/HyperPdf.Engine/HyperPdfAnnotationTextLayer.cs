// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.TextLayer;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationTextLayer annotation operations.</summary>
internal static class HyperPdfAnnotationTextLayer
{
    /// <summary>Gets the index of the built in font in the text layer's font list.</summary>
    internal const int StandardFont = 0;

    /// <summary>Writes recognised words onto a page as invisible text placed over where they appear.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written.</returns>
    internal static int AddTextLayer(HyperPdfAnnotations annotationState, int pageIndex, ReadOnlySpan<OcrWord> words)
    {
        lock (annotationState.Gate)
        {
            if (words.IsEmpty || HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page || !page.Id.IsValid)
            {
                return 0;
            }

            var fonts = new TextLayerFonts(annotationState.Store, annotationState.Catalog ?? FontCatalog.System, annotationState.LazyFontState);
            var layer = ArrayPool<PdfTextLayerWord>.Shared.Rent(words.Length);
            try
            {
                var count = fonts.Collect(words, layer);
                if (count == 0)
                {
                    return 0;
                }

                var written = fonts.Append(page, layer.AsSpan(0, count));
                _ = HyperPdfAnnotationReading.Changed(annotationState, pageIndex, written > 0);
                return written;
            }
            finally
            {
                ArrayPool<PdfTextLayerWord>.Shared.Return(layer, true);
            }
        }
    }

    /// <summary>The fonts one text layer is written in: the built in font, and a subset of each installed font its other words need.</summary>
    /// <param name="store">The document.</param>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="fontState">The viewer engine's reusable font state, built only when an installed font is needed.</param>
    [DebuggerDisplay("TextLayerFonts: {_groups} embedded")]
    private struct TextLayerFonts(PdfObjectStore store, FontCatalog catalog, Lazy<TextBoxFontState> fontState)
    {
        /// <summary>The installed fonts needed, by font program; made when the first word needs one, since most text only needs the built in font.</summary>
        private Dictionary<FontProgram, Group>? _groups;

        /// <summary>Converts the words to the writer's form, choosing each word's font.</summary>
        /// <param name="words">The recognised words.</param>
        /// <param name="output">Receives the words that can be placed.</param>
        /// <returns>The number of words placed in <paramref name="output"/>.</returns>
        internal int Collect(ReadOnlySpan<OcrWord> words, PdfTextLayerWord[] output)
        {
            var count = 0;
            foreach (ref readonly var word in words)
            {
                var bounds = word.Bounds;
                var item = new PdfTextLayerWord(word.Text, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, HyperPdfAnnotationTextLayer.StandardFont);
                if (!PdfTextLayer.IsPlaceable(item))
                {
                    continue;
                }

                var font = Assign(word.Text);
                if (font < 0)
                {
                    continue;
                }

                output[count] = item with { Font = font };
                count++;
            }

            return count;
        }

        /// <summary>Embeds the installed fonts needed and writes the words with every font listed by index.</summary>
        /// <param name="page">The page.</param>
        /// <param name="words">The words to place.</param>
        /// <returns>The number of words written. A font that cannot be embedded is replaced by the built in one, which skips its words.</returns>
        internal readonly int Append(PdfPage page, ReadOnlySpan<PdfTextLayerWord> words)
        {
            var standard = new PdfStandardTextLayerFont(store, AppearanceFont.Helvetica);
            if (_groups is null)
            {
                // Words in the built in font alone need a list of one font, which stays on the stack.
                return PdfTextLayer.Append(store, page, words, [standard]);
            }

            var fonts = new IPdfTextLayerFont[_groups.Count + 1];
            fonts[HyperPdfAnnotationTextLayer.StandardFont] = standard;

            // Pairs rather than the values collection, so no collection object is made to walk them.
            foreach (var (_, group) in _groups)
            {
                fonts[group.Index + 1] = (IPdfTextLayerFont?)group.Embed(store, fontState.Value) ?? standard;
            }

            return PdfTextLayer.Append(store, page, words, fonts);
        }

        /// <summary>Chooses the font for a word, noting the characters an installed font must hold.</summary>
        /// <param name="text">The word.</param>
        /// <returns>The font index, or -1 when no font can show the word.</returns>
        private int Assign(string text)
        {
            if (PdfStandardTextLayerFont.Covers(text))
            {
                return HyperPdfAnnotationTextLayer.StandardFont;
            }

            if (TextBoxFonts.Resolve(fontState.Value, catalog, TextFormat.Default, text) is not { Program: { } program })
            {
                return -1;
            }

            var groups = _groups ??= [];
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, program, out var exists);
            if (!exists)
            {
                slot = new(program, groups.Count - 1);
            }

            var group = slot!;
            foreach (var rune in text.EnumerateRunes())
            {
                var glyph = program.GlyphFor(rune.Value);
                if (glyph == 0)
                {
                    continue;
                }

                group.GlyphByRune[rune.Value] = glyph;
            }

            return group.Index + 1;
        }
    }

    /// <summary>The characters one installed font must show.</summary>
    /// <param name="Program">The font.</param>
    /// <param name="Index">Its place among the embedded fonts, from zero.</param>
    [DebuggerDisplay("Group: {Program.Face.Family} ({GlyphByRune.Count} characters)")]
    private sealed record Group(FontProgram Program, int Index)
    {
        /// <summary>Gets the glyph of each character.</summary>
        internal Dictionary<int, ushort> GlyphByRune { get; } = [];

        /// <summary>Embeds a subset of the font holding the characters, with a text map.</summary>
        /// <param name="documentStore">The document.</param>
        /// <param name="fontState">The viewer engine's reusable font state.</param>
        /// <returns>The font, or <see langword="null"/> when the font cannot be embedded.</returns>
        internal PdfCodedTextLayerFont? Embed(PdfObjectStore documentStore, TextBoxFontState fontState)
        {
            if (fontState.GetSource(Program).Source is not { } source)
            {
                return null;
            }

            var embedded = PdfEmbeddedFont.Create(documentStore, source);
            var codes = new Dictionary<int, ushort>(GlyphByRune.Count);
            foreach (var (rune, glyph) in GlyphByRune)
            {
                embedded.Register(char.ConvertFromUtf32(rune), [new PdfShapedGlyph(glyph, 0, 0, 0, 0)]);
                codes[rune] = (ushort)embedded.GetCode(glyph);
            }

            embedded.Finish();
            var last = 0;
            foreach (var code in codes.Values)
            {
                last = Math.Max(last, code);
            }

            var widths = new float[last + 1];
            foreach (var (rune, glyph) in GlyphByRune)
            {
                widths[codes[rune]] = MathF.Round(source.GetAdvance(glyph));
            }

            return new(embedded.Resource, codes, widths, Program.Ascent, Math.Abs(Program.Descent));
        }
    }
}
