// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>What a test font with CFF outlines contains.</summary>
public sealed record TestCffOptions
{
    /// <summary>Gets a value indicating whether the font is CID-keyed with two Font DICTs; otherwise it is name-keyed.</summary>
    public bool CidKeyed { get; init; }

    /// <summary>Gets a value indicating whether 'é' is drawn by the accented-character form of <c>endchar</c> (name-keyed fonts only).</summary>
    public bool Accents { get; init; } = true;

    /// <summary>Gets a value indicating whether the font has vertical metrics.</summary>
    public bool Vertical { get; init; }

    /// <summary>Gets the licence flags (OS/2 <c>fsType</c>).</summary>
    public int EmbeddingFlags { get; init; }

    /// <summary>Gets the tags of extra four-byte tables to add, such as <c>fvar</c> or <c>CFF2</c> to make a font that cannot be embedded.</summary>
    public IReadOnlyList<string> ExtraTables { get; init; } = [];

    /// <summary>Gets the family name.</summary>
    public string Family { get; init; } = "PVL Test Sans CFF";
}
