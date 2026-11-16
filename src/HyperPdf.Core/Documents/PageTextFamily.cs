// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The family of font new text is set in. The fonts are embedded in the document, so the text keeps its look and takes any letter the font has.</summary>
public enum PageTextFamily
{
    /// <summary>A sans serif font.</summary>
    Sans = 0,

    /// <summary>A serif font.</summary>
    Serif = 1,

    /// <summary>A fixed width font.</summary>
    Fixed = 2,
}
