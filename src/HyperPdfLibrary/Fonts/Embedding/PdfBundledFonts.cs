// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// The fonts embedded in the library assembly, which a created document can embed without any file or network access.
/// Each face is cut down to Basic Latin, Latin-1, Latin Extended-A and -B, Greek, Cyrillic, Hebrew, Vietnamese,
/// punctuation, currency, arrows, mathematical operators, box drawing and geometric shapes (the symbol families to the
/// characters of the Symbol and ZapfDingbats fonts). Text outside those ranges draws the font's missing glyph. Use
/// <c>Open</c> with <c>PdfDocumentCreator.EmbedFont</c> to put one in a document.
/// </summary>
public static class PdfBundledFonts
{
    /// <summary>Opens an embedded font without retaining global state. Reuse the returned source across documents.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether to use the bold style; ignored for symbol, dingbats, and emoji families.</param>
    /// <param name="italic">Whether to use the italic style; ignored for symbol, dingbats, and emoji families.</param>
    /// <returns>The font, or the reason it cannot be embedded.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> is not a defined family.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFontOpenResult Open(PdfBundledFontFamily family, bool bold, bool italic) =>
        Open(new PdfFontResources(), family, bold, italic);

    /// <summary>Opens an embedded font shared by documents using the supplied drawing services.</summary>
    /// <param name="services">The host's drawing services.</param>
    /// <param name="family">The font family.</param>
    /// <param name="bold">Whether to use the bold style.</param>
    /// <param name="italic">Whether to use the italic style.</param>
    /// <returns>The font, or the reason it cannot be embedded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> is not a defined family.</exception>
    public static PdfFontOpenResult Open(PdfDrawingServices services, PdfBundledFontFamily family, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(services);
        return Open(services.FontResources, family, bold, italic);
    }

    /// <summary>Opens an embedded font, throwing when it cannot be embedded.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether to use the bold style; ignored for symbol, dingbats, and emoji families.</param>
    /// <param name="italic">Whether to use the italic style; ignored for symbol, dingbats, and emoji families.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="family"/> is not a defined family.</exception>
    /// <exception cref="PdfException">The font cannot be embedded; the message gives the reason.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFontSource OpenOrThrow(PdfBundledFontFamily family, bool bold, bool italic) =>
        PdfFontOpenResult.SourceOrThrow(Open(family, bold, italic));

    /// <summary>Opens the default license-compatible bundled backup fonts (Symbol, Dingbats, and Emoji) in fallback order.</summary>
    /// <returns>The ordered list of bundled fallback font sources.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfFontSource> OpenDefaultFallbacks() =>
        OpenDefaultFallbacks(new PdfFontResources());

    /// <summary>
    /// Opens the default license-compatible bundled backup fonts (<see cref="PdfBundledFontFamily.Symbol"/>,
    /// <see cref="PdfBundledFontFamily.Dingbats"/>, and <see cref="PdfBundledFontFamily.Emoji"/>) in fallback order,
    /// shared by documents using the supplied drawing services.
    /// </summary>
    /// <param name="services">The host's drawing services.</param>
    /// <returns>The ordered list of bundled fallback font sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IReadOnlyList<PdfFontSource> OpenDefaultFallbacks(PdfDrawingServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return OpenDefaultFallbacks(services.FontResources);
    }

    /// <summary>Opens the default bundled fallback font sources using explicitly owned resources.</summary>
    /// <param name="resources">The owning resources.</param>
    /// <returns>The ordered list of bundled fallback font sources.</returns>
    internal static IReadOnlyList<PdfFontSource> OpenDefaultFallbacks(PdfFontResources resources) =>
    [
        Open(resources, PdfBundledFontFamily.Symbol, false, false).Source!,
        Open(resources, PdfBundledFontFamily.Dingbats, false, false).Source!,
        Open(resources, PdfBundledFontFamily.Emoji, false, false).Source!,
    ];

    /// <summary>Opens an embedded font using explicitly owned resources.</summary>
    /// <param name="resources">The owning resources.</param>
    /// <param name="family">The font family.</param>
    /// <param name="bold">Whether to use the bold style.</param>
    /// <param name="italic">Whether to use the italic style.</param>
    /// <returns>The cached font result.</returns>
    internal static PdfFontOpenResult Open(PdfFontResources resources, PdfBundledFontFamily family, bool bold, bool italic)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan((int)family, (int)PdfBundledFontFamily.Sans);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)family, (int)PdfBundledFontFamily.Emoji);

        // The public and internal families share their numbering.
        var internalFamily = (BundledFamily)(int)family;
        var slot = BundledFaces.SlotOf(internalFamily, bold, italic);
        return Volatile.Read(ref resources.Faces.Opened[slot]) ?? Create(resources, internalFamily, bold, italic, slot);
    }

    /// <summary>Opens a face and publishes it.</summary>
    /// <param name="resources">The owning resources.</param>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether the style is bold.</param>
    /// <param name="italic">Whether the style is italic.</param>
    /// <param name="slot">The face's slot.</param>
    /// <returns>The shared result.</returns>
    private static PdfFontOpenResult Create(PdfFontResources resources, BundledFamily family, bool bold, bool italic, int slot)
    {
        var program = BundledFaces.Program(resources, family, bold, italic);
        var result = program is null
            ? new PdfFontOpenResult(null, "The font embedded in the library is damaged.")
            : PdfFontSource.Open(program);
        return Interlocked.CompareExchange(ref resources.Faces.Opened[slot], result, null) ?? result;
    }
}
