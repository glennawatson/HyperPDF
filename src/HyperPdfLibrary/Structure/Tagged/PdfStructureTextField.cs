// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The text entries of a structure element that an editor can set (ISO 32000-2 14.7.2, Table 355).</summary>
public enum PdfStructureTextField
{
    /// <summary>The <c>/Alt</c> description a screen reader says in place of the content.</summary>
    AlternateText = 0,

    /// <summary>The <c>/ActualText</c> that replaces the content when it is copied or read.</summary>
    ActualText = 1,

    /// <summary>The <c>/E</c> expansion of an abbreviation.</summary>
    Expansion = 2,

    /// <summary>The <c>/T</c> title.</summary>
    Title = 3,

    /// <summary>The <c>/Lang</c> language tag.</summary>
    Language = 4,
}
