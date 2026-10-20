// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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

    /// <summary>
    /// Searches a page's collected runs without making a text page: it keeps only the page text and the character index of
    /// each text character in pooled buffers, and reports the same matches a text page's Find would. Then it clears the state.
    /// </summary>
    /// <typeparam name="TState">The type of the caller's state.</typeparam>
    /// <param name="state">The reusable build state.</param>
    /// <param name="rightToLeft">Whether the document asks for right-to-left reading order.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="caller">The caller's state passed to each visit.</param>
    /// <param name="visit">Receives each match's first character and length.</param>
    internal static void Find<TState>(TextPageBuildState state, bool rightToLeft, ReadOnlySpan<char> query, PdfTextSearchOptions options, TState caller, Action<TState, int, int> visit)
    {
        state.RightToLeft = rightToLeft;
        char[]? text = null;
        int[]? charOfText = null;
        try
        {
            TextLineGrouping.Assemble(state);
            var source = CollectionsMarshal.AsSpan(state.Chars);
            var length = CountText(source);
            if (length == 0 || query.IsEmpty)
            {
                return;
            }

            text = ArrayPool<char>.Shared.Rent(length);
            charOfText = ArrayPool<int>.Shared.Rent(length);
            FillText(source, text, charOfText);
            using var folded = TextFolding.FoldPooled(text.AsSpan(0, length), options & TextFolding.FoldOptions);
            TextSearch.FindAll(folded.View, query, options, new SearchSink<TState>(caller, visit, charOfText), static (sink, first, last) => sink.Add(first, last));
        }
        finally
        {
            state.Clear();
            ReturnRented(text, charOfText);
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
        var styles = BuildStyles(state, source);
        return new(page, chars, lines, BuildLevels(source), styles with { Decorations = TextDecorationScan.Detect(chars, styles, state.Device.Marks) }, new(text), charOfText);
    }

    /// <summary>Keeps the style of each run and the run of each character.</summary>
    /// <param name="state">The build state, whose runs and fonts are read.</param>
    /// <param name="source">The characters the build collected.</param>
    /// <returns>The style table.</returns>
    private static TextStyleTable BuildStyles(TextPageBuildState state, ReadOnlySpan<TextBuildChar> source)
    {
        var styles = new TextRunStyle[state.Runs.Count];
        for (var i = 0; i < styles.Length; i++)
        {
            var run = state.Runs[i];
            ref var facts = ref CollectionsMarshal.GetValueRefOrAddDefault(state.FontFacts, run.Font, out var known);
            if (!known)
            {
                facts = TextFontFacts.Read(run.Font);
            }

            styles[i] = new(run.Matrix, run.FillColor, run.StrokeColor, facts.Name, facts.Weight, facts.Flags);
        }

        var runOfChar = new int[source.Length];
        for (var i = 0; i < runOfChar.Length; i++)
        {
            runOfChar[i] = (uint)source[i].Run < (uint)styles.Length ? source[i].Run : -1;
        }

        return new(styles, runOfChar, null);
    }

    /// <summary>Counts the characters that are part of the page text.</summary>
    /// <param name="source">The characters the build collected.</param>
    /// <returns>The count.</returns>
    private static int CountText(ReadOnlySpan<TextBuildChar> source)
    {
        var count = 0;
        foreach (var info in source)
        {
            count += info.InText ? 1 : 0;
        }

        return count;
    }

    /// <summary>Returns the buffers a search rented.</summary>
    /// <param name="text">The text buffer, or <see langword="null"/>.</param>
    /// <param name="charOfText">The index buffer, or <see langword="null"/>.</param>
    private static void ReturnRented(char[]? text, int[]? charOfText)
    {
        if (text is not null)
        {
            ArrayPool<char>.Shared.Return(text);
        }

        if (charOfText is not null)
        {
            ArrayPool<int>.Shared.Return(charOfText);
        }
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

    /// <summary>Turns a match in the page text into a match of page characters.</summary>
    /// <typeparam name="TState">The type of the caller's state.</typeparam>
    /// <param name="State">The caller's state.</param>
    /// <param name="Visit">The caller's method receiving each match's first character and length.</param>
    /// <param name="CharOfText">The character index of each page text index.</param>
    private readonly record struct SearchSink<TState>(TState State, Action<TState, int, int> Visit, int[] CharOfText)
    {
        /// <summary>Reports a match.</summary>
        /// <param name="first">The text index of the first character.</param>
        /// <param name="last">The text index of the last character.</param>
        internal void Add(int first, int last)
        {
            var start = CharOfText[first];
            Visit(State, start, CharOfText[last] - start + 1);
        }
    }
}
