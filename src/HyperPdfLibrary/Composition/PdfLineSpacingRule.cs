// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>How the value of a paragraph's line spacing is read.</summary>
public enum PdfLineSpacingRule
{
    /// <summary>The value is a multiple of the line's natural height: 1 is single spacing and 1.5 is one and a half.</summary>
    Multiple = 0,

    /// <summary>Every line is exactly the value in points high.</summary>
    Exactly = 1,

    /// <summary>Every line is at least the value in points high, and taller when its text needs it.</summary>
    AtLeast = 2,
}
