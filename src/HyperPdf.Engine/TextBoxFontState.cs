// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Engine;

/// <summary>Remembers font shapers and fallback searches for documents opened by one viewer engine.</summary>
internal sealed class TextBoxFontState
{
    /// <summary>The code point stored for a face whose file cannot be used.</summary>
    private const int UnreadableMarker = -1;

    /// <summary>The combinations of bold and italic.</summary>
    private const int StyleCount = 4;

    /// <summary>The slot offset of a bold style among the four styles.</summary>
    private const int BoldSlot = 2;

    /// <summary>The installed faces retained for documents opened by this engine.</summary>
    private readonly ConcurrentDictionary<FaceKey, FontProgram> _programs = new();

    /// <summary>The characters faces were found not to have, so a search for a font does not read the same files again.</summary>
    private readonly ConcurrentDictionary<MissKey, byte> _misses = new();

    /// <summary>The installed font catalog for documents opened by this engine.</summary>
    private readonly Lazy<FontCatalog> _catalog;

    /// <summary>The faces of the last catalog used, in the order to try them.</summary>
    private RankedSet? _ranked;

    /// <summary>Initializes a new instance of the <see cref="TextBoxFontState"/> class with its own catalog.</summary>
    internal TextBoxFontState()
        : this(new Lazy<FontCatalog>(static () => FontCatalog.Scan(FontCatalog.SystemDirectories())))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TextBoxFontState"/> class with an engine catalog.</summary>
    /// <param name="catalog">The installed fonts, scanned on first use.</param>
    internal TextBoxFontState(Lazy<FontCatalog> catalog) => _catalog = catalog;

    /// <summary>Gets the installed font catalog, scanning on first use.</summary>
    internal FontCatalog Catalog => _catalog.Value;

    /// <summary>Gets the standard-font shapers used by this engine.</summary>
    internal StandardTextShaper?[] StandardShapers { get; } = new StandardTextShaper?[StandardTextShaper.FontCount];

    /// <summary>Gets the shaper of each installed font used by the document.</summary>
    internal ConcurrentDictionary<FontProgram, FontProgramShaper> Shapers { get; } = new();

    /// <summary>Gets the library's view of each installed font used by the document, which says whether it can be embedded.</summary>
    internal ConcurrentDictionary<FontProgram, PdfFontOpenResult> Sources { get; } = new();

    /// <summary>Gets fallback faces without keeping discarded font catalogs alive.</summary>
    internal ConditionalWeakTable<FontCatalog, ConcurrentDictionary<FallbackKey, FontFace>> Found { get; } = new();

    /// <summary>Gets the library's view of an installed font: the font to embed, or the plain reason it is refused.</summary>
    /// <param name="program">The font.</param>
    /// <returns>The result of opening the font file for embedding.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfFontOpenResult GetSource(FontProgram program) =>
        Sources.GetOrAdd(program, static font => PdfFontSource.Open(font.Data, font.Face.FaceIndex));

    /// <summary>Loads a face once within this engine's lifetime.</summary>
    /// <param name="face">The installed face.</param>
    /// <returns>The program, or null when its file is unreadable.</returns>
    internal FontProgram? Load(FontFace face)
    {
        var key = new FaceKey(face.Path, face.FaceIndex);
        if (_programs.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var loaded = FontProgram.Load(face);
        return loaded is null ? null : _programs.GetOrAdd(key, loaded);
    }

    /// <summary>Retains a face already read while searching fallback fonts.</summary>
    /// <param name="program">The read face.</param>
    /// <returns>The face retained by this engine.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FontProgram Remember(FontProgram program) =>
        _programs.GetOrAdd(new(program.Face.Path, program.Face.FaceIndex), program);

    /// <summary>Finds an already loaded face without reading its file.</summary>
    /// <param name="face">The face.</param>
    /// <param name="program">The retained program, when present.</param>
    /// <returns>Whether the face was already loaded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetLoaded(FontFace face, out FontProgram? program) =>
        _programs.TryGetValue(new(face.Path, face.FaceIndex), out program);

    /// <summary>Gets the installed faces in the order to try them for a style, ordered once for each style and catalog.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="bold">Whether bold is wanted.</param>
    /// <param name="italic">Whether italic is wanted.</param>
    /// <returns>The faces, best match first.</returns>
    internal FontFace[] RankedFaces(FontCatalog catalog, bool bold, bool italic)
    {
        var ranked = Volatile.Read(ref _ranked);
        if (ranked is null || !ReferenceEquals(ranked.Catalog, catalog))
        {
            ranked = new(catalog, new FontFace[StyleCount][]);
            Volatile.Write(ref _ranked, ranked);
        }

        return ranked.Orders[(bold ? BoldSlot : 0) | (italic ? 1 : 0)] ??= TextBoxFonts.Rank(catalog, bold, italic);
    }

    /// <summary>Tells whether a face is already known to lack a character of some text, or to be unreadable, so its file need not be read again.</summary>
    /// <param name="face">The face.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether the face cannot show the text.</returns>
    internal bool KnownToLack(FontFace face, string text)
    {
        if (_misses.IsEmpty)
        {
            return false;
        }

        if (_misses.ContainsKey(new(face.Path, face.FaceIndex, UnreadableMarker)))
        {
            return true;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (_misses.ContainsKey(new(face.Path, face.FaceIndex, rune.Value)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Remembers that a face has no glyph for a character.</summary>
    /// <param name="face">The face.</param>
    /// <param name="codePoint">The character.</param>
    internal void NoteLacks(FontFace face, int codePoint) => _ = _misses.TryAdd(new(face.Path, face.FaceIndex, codePoint), 0);

    /// <summary>Remembers that a face's file cannot be read or is too large to try.</summary>
    /// <param name="face">The face.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void NoteUnreadable(FontFace face) => NoteLacks(face, UnreadableMarker);

    /// <summary>A fallback lookup within the font catalog that owns its table.</summary>
    /// <param name="Block">The block of the first character outside Latin-1.</param>
    /// <param name="Bold">Whether bold is wanted.</param>
    /// <param name="Italic">Whether italic is wanted.</param>
    internal readonly record struct FallbackKey(int Block, bool Bold, bool Italic);

    /// <summary>The file and face number of an installed font.</summary>
    /// <param name="Path">The font file.</param>
    /// <param name="FaceIndex">The face within it.</param>
    private readonly record struct FaceKey(string Path, int FaceIndex);

    /// <summary>A character a face is known not to have.</summary>
    /// <param name="Path">The font file.</param>
    /// <param name="FaceIndex">The face within it.</param>
    /// <param name="CodePoint">The character, or the marker for a face that cannot be read.</param>
    private readonly record struct MissKey(string Path, int FaceIndex, int CodePoint);

    /// <summary>The faces of a catalog in the order to try them for each of the four styles.</summary>
    /// <param name="Catalog">The catalog.</param>
    /// <param name="Orders">The order for each style, filled in as styles are asked for.</param>
    private sealed record RankedSet(FontCatalog Catalog, FontFace[][] Orders);
}
