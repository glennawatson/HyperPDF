// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The child types that ISO/TS 32005 Table 5 allows at most once in a parent (occurrence 0..1).</summary>
[Flags]
internal enum OnceChild
{
    /// <summary>None of them.</summary>
    None = 0,

    /// <summary>A Caption.</summary>
    Caption = 1 << 0,

    /// <summary>A THead.</summary>
    Head = 1 << 2,

    /// <summary>A TFoot.</summary>
    Foot = 1 << 3,

    /// <summary>An Art in a heading.</summary>
    Article = 1 << 4,

    /// <summary>A Sect in a heading.</summary>
    Section = 1 << 5,
}
