// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>How much text a screen reader asks for at a position.</summary>
public enum TextGranularity
{
    /// <summary>One character.</summary>
    Character = 0,

    /// <summary>A word with the spaces after it, up to the start of the next word.</summary>
    Word = 1,

    /// <summary>A sentence with the spaces after it, up to the start of the next sentence.</summary>
    Sentence = 2,

    /// <summary>A line, up to and including its line feed.</summary>
    Line = 3,

    /// <summary>A paragraph, up to and including its line feed.</summary>
    Paragraph = 4,
}
