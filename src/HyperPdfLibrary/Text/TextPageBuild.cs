// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Text;

/// <summary>Builds text pages with independently leased state for each extraction.</summary>
internal static class TextPageBuild
{
    /// <summary>The font size reported for characters that have no run.</summary>
    private const float DefaultFontSize = 1;

    /// <summary>The most glyphs or characters a state's lists may have grown to and still be kept for other documents: 64 Ki.</summary>
    private const int MaxSharedItems = 1 << 16;

    /// <summary>Gets a cleaned state from the document, then from closed documents, or makes one when none is free.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <returns>The state exclusively owned by this extraction.</returns>
    internal static TextPageBuildState Rent(PdfDocument document) => Interlocked.Exchange(ref document.State.TextBuild, null) ?? document.DrawingServices.TextBuildStates.Take() ?? new();

    /// <summary>Offers a cleaned state to its document for another extraction.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <param name="state">The cleaned state.</param>
    internal static void Return(PdfDocument document, TextPageBuildState state)
    {
        if (document.IsDisposed || Interlocked.CompareExchange(ref document.State.TextBuild, state, null) is not null)
        {
            Share(document, state);
            return;
        }

        if (document.IsDisposed && Interlocked.CompareExchange(ref document.State.TextBuild, null, state) == state)
        {
            Share(document, state);
        }
    }

    /// <summary>Moves a closing document's state to the states other documents may take.</summary>
    /// <param name="document">The closing document.</param>
    internal static void Release(PdfDocument document)
    {
        if (Interlocked.Exchange(ref document.State.TextBuild, null) is { } state)
        {
            Share(document, state);
        }
    }

    /// <summary>Builds a text page from the collected runs, then clears the state.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="page">The page.</param>
    /// <param name="rightToLeft">Whether the document asks for right-to-left reading order.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage Build(TextPageBuildState state, PdfPage page, bool rightToLeft)
    {
        state.RightToLeft = rightToLeft;
        try
        {
            TextLineGrouping.Assemble(state);
            return CreatePage(state, page);
        }
        finally
        {
            state.Clear();
        }
    }

    /// <summary>Keeps a cleaned state for other documents unless its lists grew very large or every slot is full.</summary>
    /// <param name="document">The document whose host owns the shared states.</param>
    /// <param name="state">The cleaned state.</param>
    private static void Share(PdfDocument document, TextPageBuildState state)
    {
        if (state.Glyphs.Capacity > MaxSharedItems || state.Unicode.Capacity > MaxSharedItems || state.Chars.Capacity > MaxSharedItems)
        {
            return;
        }

        _ = document.DrawingServices.TextBuildStates.Offer(state);
    }

    /// <summary>Makes the finished page from the characters the build collected.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text page.</returns>
    private static PdfTextPage CreatePage(TextPageBuildState state, PdfPage page)
    {
        var source = CollectionsMarshal.AsSpan(state.Chars);
        var chars = new PdfTextChar[source.Length];
        var lines = new int[source.Length];
        var textLength = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var info = source[i];
            var hasRun = info.Run >= 0 && info.Run < state.Runs.Count;
            var run = hasRun ? state.Runs[info.Run] : default;
            lines[i] = info.Line;
            textLength += info.InText ? 1 : 0;
            chars[i] = new(info.Unicode, info.Kind, info.Code, info.Origin, info.Box, info.LooseBox, hasRun ? run.FontSize : DefaultFontSize, hasRun ? run.RenderMode : 0, hasRun && run.Font.IsBold);
        }

        var text = new char[textLength];
        var charOfText = new int[textLength];
        FillText(source, text, charOfText);
        return new(page, chars, lines, BuildLevels(source), new(text), charOfText);
    }

    /// <summary>Copies the characters that are part of the page text, and the character index of each.</summary>
    /// <param name="source">The characters the build collected.</param>
    /// <param name="text">Receives the text.</param>
    /// <param name="charOfText">Receives the character index of each text index.</param>
    private static void FillText(ReadOnlySpan<TextBuildChar> source, char[] text, int[] charOfText)
    {
        var next = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (!source[i].InText)
            {
                continue;
            }

            text[next] = source[i].Unicode;
            charOfText[next] = i;
            next++;
        }
    }

    /// <summary>Collects the embedding level of each character.</summary>
    /// <param name="source">The characters the build collected.</param>
    /// <returns>The levels, or <see langword="null"/> when every character is at level 0, as on pages of left-to-right text.</returns>
    private static byte[]? BuildLevels(ReadOnlySpan<TextBuildChar> source)
    {
        byte[]? levels = null;
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i].Level == 0)
            {
                continue;
            }

            levels ??= new byte[source.Length];
            levels[i] = source[i].Level;
        }

        return levels;
    }
}
