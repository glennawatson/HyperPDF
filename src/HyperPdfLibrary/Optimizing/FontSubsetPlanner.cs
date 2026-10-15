// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Works out which glyphs each embedded TrueType program must keep. The pages are run as for text extraction, and every
/// code a font shows is mapped to its glyph the way the renderer maps it; for simple fonts the glyphs every cmap
/// subtable gives the code are kept too, so readers that choose a different subtable still find their glyph. A program
/// is subset only when every font using it is shown only by page content: fonts used by form fields, annotations,
/// patterns or soft masks keep every glyph, as do fonts no page shows.
/// </summary>
internal static class FontSubsetPlanner
{
    /// <summary>The most characters one code's text is read into.</summary>
    private const int MaxUnicode = 8;

    /// <summary>Plans the subsets.</summary>
    /// <param name = "document">The working copy.</param>
    /// <param name = "scanner">What the content analysis learned.</param>
    /// <param name = "graph">The reachable objects.</param>
    /// <param name = "state">The tables the plan fills; the glyphs to keep end up in <see cref="FontPlanState.Kept"/>.</param>
    /// <param name = "report">Receives what was skipped.</param>
    /// <param name="cancellationToken">Stops page interpretation.</param>
    internal static void Plan(PdfDocument document, ContentUsageScanner scanner, OptimizerGraph graph, FontPlanState state, OptimizeReportBuilder report, CancellationToken cancellationToken)
    {
        FindPrograms(graph, state, report);
        if (state.Programs.Count == 0)
        {
            return;
        }

        using var device = FontUsageDevice.Rent(document.DrawingServices.Optimization);
        if (!RunPages(document, device, cancellationToken))
        {
            report.Skip(PdfOptimizeCategory.Fonts, 0, "A page's content could not be read to the end, so no font was subset.");
            return;
        }

        GlyphsByFont(device, scanner, state);
        foreach (var (program, users) in state.Programs)
        {
            if (Union(program, users, state, scanner, report) is { } kept)
            {
                state.Kept[program] = kept;
            }
        }
    }

    /// <summary>Finds the embedded TrueType programs and the fonts using each.</summary>
    /// <param name = "graph">The reachable objects.</param>
    /// <param name = "state">Receives the font numbers using each program, by program number.</param>
    /// <param name = "report">Receives the fonts that cannot be subset.</param>
    private static void FindPrograms(OptimizerGraph graph, FontPlanState state, OptimizeReportBuilder report)
    {
        var programs = state.Programs;
        for (var number = 1; number <= graph.Count; number++)
        {
            if (Descriptor(graph.GetValue(number)) is not { } descriptor)
            {
                continue;
            }

            var program = descriptor.GetRaw(KnownName.FontFile2);
            if (program.IsReference)
            {
                ref var users = ref CollectionsMarshal.GetValueRefOrAddDefault(programs, program.AsReference().Number, out _);
                users ??= state.RentList();
                users.Add(graph.GetOldNumber(number));
            }
            else if (descriptor.GetRaw(KnownName.FontFile3).IsReference)
            {
                report.Skip(PdfOptimizeCategory.Fonts, descriptor.GetRaw(KnownName.FontFile3).AsReference().Number, "CFF and OpenType font programs are not subset yet.");
            }
        }
    }

    /// <summary>
    /// Gets a font's descriptor: its own, or its descendant's for a composite font. /Type is often missing, so fonts are
    /// found by their subtype; a CIDFont counts through its Type 0 parent.
    /// </summary>
    /// <param name = "value">An object that may be a font dictionary.</param>
    /// <returns>The descriptor, or <see langword="null"/> when the object is not a simple or Type 0 font.</returns>
    private static PdfDictionary? Descriptor(PdfValue value)
    {
        var font = value.Kind == PdfKind.Dictionary ? value.AsDictionary() : null;
        return font?.GetName(KnownName.Subtype).ToKnownName() switch
        {
            KnownName.Type0 => font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0)?.GetDictionary(KnownName.FontDescriptor),
            KnownName.TrueType or KnownName.Type1 => font.GetDictionary(KnownName.FontDescriptor),
            _ => null,
        };
    }

    /// <summary>Runs every page's content into the device.</summary>
    /// <param name = "document">The working copy.</param>
    /// <param name = "device">The device.</param>
    /// <param name="cancellationToken">Stops page interpretation.</param>
    /// <returns><see langword="false"/> when a page's content could not be read to the end.</returns>
    private static bool RunPages(PdfDocument document, FontUsageDevice device, CancellationToken cancellationToken)
    {
        for (var i = 0; i < document.PageCount; i++)
        {
            using var interpreter = ContentInterpreter.Rent(PdfDocumentRendering.GetRenderCache(document), device, 0, cancellationToken);
            try
            {
                ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, i));
            }
            catch (Exception e) when (e is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Maps each font's codes to glyphs.</summary>
    /// <param name = "device">The device that saw the codes.</param>
    /// <param name = "scanner">What the content analysis learned, including each font's object number.</param>
    /// <param name = "state">Receives the glyphs each font shows, by font number.</param>
    private static void GlyphsByFont(FontUsageDevice device, ContentUsageScanner scanner, FontPlanState state)
    {
        foreach (var (font, codes) in device.Codes)
        {
            if (!scanner.FontNumbers.TryGetValue(font.Dictionary, out var number))
            {
                continue;
            }

            ref var glyphs = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Shown, number, out _);
            glyphs ??= state.RentBits();
            for (var code = codes.NextBit(0); code != CodeBitSet.NoBit; code = codes.NextBit(code + 1))
            {
                AddGlyphs(font, code, glyphs);
            }

            if (codes.Overflow is { } overflow)
            {
                foreach (var code in overflow)
                {
                    AddGlyphs(font, code, glyphs);
                }
            }
        }
    }

    /// <summary>Adds the glyphs a code may be drawn with.</summary>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The code.</param>
    /// <param name = "glyphs">The glyphs.</param>
    private static void AddGlyphs(PdfFont font, int code, CodeBitSet glyphs)
    {
        switch (font)
        {
            case PdfCompositeFont composite:
                {
                    AddGlyph(glyphs, composite.GetGlyph(code));
                    break;
                }

            case PdfSimpleFont simple:
                {
                    AddGlyph(glyphs, simple.GetGlyph(code));
                    if (simple.Source is ProgramGlyphSource { Program: TrueTypeProgram program })
                    {
                        AddCmapGlyphs(program, font, code, glyphs);
                    }

                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Adds the glyph every cmap subtable gives a simple font's code, as other readers may choose any of them.</summary>
    /// <param name = "program">The font program.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The code.</param>
    /// <param name = "glyphs">The glyphs.</param>
    private static void AddCmapGlyphs(TrueTypeProgram program, PdfFont font, int code, CodeBitSet glyphs)
    {
        AddGlyph(glyphs, program.LookupMacCmap(code));
        AddGlyph(glyphs, program.LookupSymbolCmap(code));
        AddGlyph(glyphs, program.GetGlyphByCharCode(code));
        Span<char> text = stackalloc char[MaxUnicode];
        var length = Math.Min(font.GetUnicode(code, text), MaxUnicode);
        if (length <= 0 || Rune.DecodeFromUtf16(text[..length], out var rune, out _) != System.Buffers.OperationStatus.Done)
        {
            return;
        }

        AddGlyph(glyphs, program.LookupUnicodeCmap(rune.Value));
        AddGlyph(glyphs, program.GetGlyphByUnicode(rune.Value));
    }

    /// <summary>Adds a glyph when it is one.</summary>
    /// <param name = "glyphs">The glyphs.</param>
    /// <param name = "glyph">The glyph id, or a negative value for none.</param>
    private static void AddGlyph(CodeBitSet glyphs, int glyph)
    {
        if (glyph <= 0)
        {
            return;
        }

        _ = glyphs.Add(glyph);
    }

    /// <summary>Joins the glyphs of every font using a program, or explains why the program must stay whole.</summary>
    /// <param name = "program">The program's number.</param>
    /// <param name = "users">The fonts using it.</param>
    /// <param name = "state">The tables of the plan, holding the glyphs each font shows.</param>
    /// <param name = "scanner">What the content analysis learned.</param>
    /// <param name = "report">Receives the reason a program stays whole.</param>
    /// <returns>The glyphs to keep, or <see langword="null"/>.</returns>
    private static CodeBitSet? Union(int program, List<int> users, FontPlanState state, ContentUsageScanner scanner, OptimizeReportBuilder report)
    {
        var kept = state.RentBits();
        foreach (var user in users)
        {
            if (scanner.ExcludedFonts.Contains(user))
            {
                report.Skip(PdfOptimizeCategory.Fonts, program, "The font is used by form fields, annotations, patterns or soft masks, so it keeps every glyph.");
                state.Release(kept);
                return null;
            }

            if (!state.Shown.TryGetValue(user, out var glyphs))
            {
                report.Skip(PdfOptimizeCategory.Fonts, program, "No page shows text in the font, so it is left whole.");
                state.Release(kept);
                return null;
            }

            kept.UnionWith(glyphs);
        }

        return kept;
    }
}
