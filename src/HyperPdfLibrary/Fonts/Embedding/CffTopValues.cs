// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The values of a CFF Top DICT that a subset follows or keeps.</summary>
[DebuggerDisplay("CffTopValues: cid {IsCid}")]
internal sealed class CffTopValues
{
    /// <summary>Gets or sets the offset of the CharStrings INDEX.</summary>
    internal int CharStrings { get; set; }

    /// <summary>Gets or sets the charset offset, or the id of a predefined charset.</summary>
    internal int Charset { get; set; }

    /// <summary>Gets or sets the Private DICT's size.</summary>
    internal int PrivateSize { get; set; }

    /// <summary>Gets or sets the Private DICT's offset.</summary>
    internal int PrivateOffset { get; set; } = -1;

    /// <summary>Gets or sets the FDArray offset.</summary>
    internal int FdArray { get; set; }

    /// <summary>Gets or sets the FDSelect offset.</summary>
    internal int FdSelect { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the font is CID-keyed.</summary>
    internal bool IsCid { get; set; }

    /// <summary>Gets or sets the registry SID of a CID-keyed font.</summary>
    internal int Registry { get; set; }

    /// <summary>Gets or sets the ordering SID of a CID-keyed font.</summary>
    internal int Ordering { get; set; }

    /// <summary>Gets or sets the supplement of a CID-keyed font.</summary>
    internal int Supplement { get; set; }

    /// <summary>Gets or sets the <c>FontBBox</c> entry; empty when the DICT has none.</summary>
    internal CffDictEntry FontBox { get; set; }

    /// <summary>Gets or sets the <c>FontMatrix</c> entry; empty when the DICT has none.</summary>
    internal CffDictEntry FontMatrix { get; set; }
}
