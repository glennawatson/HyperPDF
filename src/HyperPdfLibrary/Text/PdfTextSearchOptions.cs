// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>Text search options.</summary>
[Flags]
public enum PdfTextSearchOptions
{
    /// <summary>Case-insensitive, partial word matching; each match starts after the previous one ends.</summary>
    None = 0,

    /// <summary>Only match whole words: the characters around a match must not be letters, digits or underscores.</summary>
    WholeWord = 1 << 0,

    /// <summary>Match letter case.</summary>
    MatchCase = 1 << 1,

    /// <summary>Ignore accents and other combining marks, so "resume" finds "résumé".</summary>
    IgnoreDiacritics = 1 << 2,

    /// <summary>Let matches overlap: look for the next match from the character after the previous match's start.</summary>
    Consecutive = 1 << 3,

    /// <summary>
    /// Treat compatibility variants as equal: the forms Unicode compatibility decomposition (NFKD) spells out, such as
    /// superscripts, fractions, circled numbers, units and halfwidth or fullwidth forms, and also curly quotes, dashes and
    /// no-break or typographic spaces. A letter and its accent match however each is written, with or without this option.
    /// </summary>
    FoldCompatibility = 1 << 4,
}
