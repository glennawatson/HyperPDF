// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Reads a font's /ToUnicode stream; a name such as /Identity-H is ignored, as PDFium ignores it.</summary>
internal static class ToUnicodeLoader
{
    /// <summary>Parses /ToUnicode.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The map, or <see langword="null"/> when the font has none or it is damaged.</returns>
    internal static ToUnicodeMap? Load(PdfDictionary font, CancellationToken cancellationToken = default)
    {
        if (font.GetStream(KnownName.ToUnicode) is not { } stream)
        {
            return null;
        }

        try
        {
            var map = ToUnicodeMap.Parse(stream.DecodeToArray(cancellationToken));
            return map.Count > 0 ? map : null;
        }
        catch (Exception ex) when (FontLoadErrors.IsDamagedData(ex))
        {
            return null;
        }
    }
}
