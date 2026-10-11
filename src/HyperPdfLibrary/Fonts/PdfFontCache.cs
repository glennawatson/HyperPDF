// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Fonts;

/// <summary>Loads each font dictionary of a document once with its configured factory.</summary>
internal sealed class PdfFontCache
{
    /// <summary>The factory selected when the document was opened.</summary>
    private readonly Func<PdfDictionary, PdfFont?>? _factory;

    /// <summary>The loaded fonts by dictionary.</summary>
    private readonly ObjectCache<PdfDictionary, PdfFont> _fonts = new();

    /// <summary>The form-encoding maps of fonts used by this cache, created only when a form needs one.</summary>
    private ObjectCache<PdfFont, FormCodeMap>? _formCodeMaps;

    /// <summary>Initializes a new instance of the <see cref="PdfFontCache"/> class.</summary>
    /// <param name="factory">The document's font loader.</param>
    internal PdfFontCache(Func<PdfDictionary, PdfFont?>? factory) => _factory = factory;

    /// <summary>Gets a font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when no factory is set or it cannot load the font.</returns>
    internal PdfFont? Get(PdfDictionary dictionary) => _factory is null ? null : _fonts.GetOrCreate(dictionary, _factory, Load);

    /// <summary>Gets the writable character codes of a loaded form font.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The map, or null for an unsupported multi-byte encoding.</returns>
    internal FormCodeMap? GetCodeMap(PdfFont font, PdfNameTable names)
    {
        var maps = Volatile.Read(ref _formCodeMaps);
        if (maps is null)
        {
            var created = new ObjectCache<PdfFont, FormCodeMap>();
            maps = Interlocked.CompareExchange(ref _formCodeMaps, created, null) ?? created;
        }

        return maps.GetOrCreate(font, names, FormCodeMap.Create);
    }

    /// <summary>Forgets every loaded font. Renders that already hold a font keep using it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear()
    {
        _fonts.Clear();
        Volatile.Read(ref _formCodeMaps)?.Clear();
    }

    /// <summary>Loads a font, treating a damaged font as missing.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="load">The factory.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private static PdfFont? Load(PdfDictionary dictionary, Func<PdfDictionary, PdfFont?> load)
    {
        try
        {
            return load(dictionary);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
