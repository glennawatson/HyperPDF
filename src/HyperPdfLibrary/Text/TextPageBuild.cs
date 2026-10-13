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

    /// <summary>The cleaned states kept for any document once their document is closed.</summary>
    private const int SharedSlots = 4;

    /// <summary>The most glyphs or characters a state's lists may have grown to and still be kept for other documents: 64 Ki.</summary>
    private const int MaxSharedItems = 1 << 16;

    /// <summary>The cleaned states of closed documents, so the next document starts with lists already grown.</summary>
    private static readonly TextPageBuildState?[] SharedStates = new TextPageBuildState?[SharedSlots];

    /// <summary>Gets a cleaned state from the document, then from closed documents, or makes one when none is free.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <returns>The state exclusively owned by this extraction.</returns>
    internal static TextPageBuildState Rent(PdfDocument document) => Interlocked.Exchange(ref document.State.TextBuild, null) ?? RentShared() ?? new();

    /// <summary>Offers a cleaned state to its document for another extraction.</summary>
    /// <param name="document">The document that owns the reusable state.</param>
    /// <param name="state">The cleaned state.</param>
    internal static void Return(PdfDocument document, TextPageBuildState state)
    {
        if (document.IsDisposed || Interlocked.CompareExchange(ref document.State.TextBuild, state, null) is not null)
        {
            Share(state);
            return;
        }

        if (document.IsDisposed && Interlocked.CompareExchange(ref document.State.TextBuild, null, state) == state)
        {
            Share(state);
        }
    }

    /// <summary>Moves a closing document's state to the states other documents may take.</summary>
    /// <param name="document">The closing document.</param>
    internal static void Release(PdfDocument document)
    {
        if (Interlocked.Exchange(ref document.State.TextBuild, null) is { } state)
        {
            Share(state);
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

    /// <summary>Takes a cleaned state from the closed documents.</summary>
    /// <returns>The state, or <see langword="null"/> when none is free.</returns>
    private static TextPageBuildState? RentShared()
    {
        for (var i = 0; i < SharedStates.Length; i++)
        {
            if (Volatile.Read(ref SharedStates[i]) is not null && Interlocked.Exchange(ref SharedStates[i], null) is { } state)
            {
                return state;
            }
        }

        return null;
    }

    /// <summary>Keeps a cleaned state for other documents unless its lists grew very large or every slot is full.</summary>
    /// <param name="state">The cleaned state.</param>
    private static void Share(TextPageBuildState state)
    {
        if (state.Glyphs.Capacity > MaxSharedItems || state.Unicode.Capacity > MaxSharedItems || state.Chars.Capacity > MaxSharedItems)
        {
            return;
        }

        for (var i = 0; i < SharedStates.Length; i++)
        {
            if (Interlocked.CompareExchange(ref SharedStates[i], state, null) is null)
            {
                return;
            }
        }
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

        return new(page, chars, lines, new(text), charOfText);
    }
}
