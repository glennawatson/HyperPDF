// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>A family of the fonts embedded in the library, each standing in for a family of the 14 standard fonts.</summary>
public enum PdfBundledFontFamily
{
    /// <summary>Arimo, with the metrics of Helvetica and Arial.</summary>
    Sans = 0,

    /// <summary>Tinos, with the metrics of Times and Times New Roman.</summary>
    Serif = 1,

    /// <summary>Cousine, with the metrics of Courier and Courier New.</summary>
    Fixed = 2,

    /// <summary>Noto Sans Math, cut down to the characters of the Symbol font; it has one style.</summary>
    Symbol = 3,

    /// <summary>Noto Sans Symbols 2, cut down to the characters of ZapfDingbats; it has one style.</summary>
    Dingbats = 4,

    /// <summary>Noto Emoji, a monochrome outline font covering Unicode emoji and composite sequences; it has one style.</summary>
    Emoji = 5,
}
