// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>What extraction does with a hyphen at the end of a line that splits a word.</summary>
public enum PdfTextHyphenMode
{
    /// <summary>The word is joined and the hyphen is left out, so "hy-" and "phen" read as "hyphen". The hyphen keeps a character slot.</summary>
    Join = 0,

    /// <summary>The hyphen stays in the text and the line break stays too, as on the page.</summary>
    Keep = 1,
}
