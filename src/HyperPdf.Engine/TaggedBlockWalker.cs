// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>
/// Turns a page's tagged reading nodes into <see cref="TaggedBlock"/> values the way the PDFium engine does: grouping
/// elements are walked into, every other element becomes one block of the characters of its content and its
/// descendants', and an element with no characters and no replacement text is dropped.
/// </summary>
[DebuggerDisplay("TaggedBlockWalker: {Blocks.Count} blocks")]
internal sealed class TaggedBlockWalker
{
    /// <summary>The code of a grouping type in <see cref="Kinds"/>.</summary>
    private const byte Grouping = 0xFF;

    /// <summary>The code of a type that is not in PDFium's table, read as a paragraph unless it has child elements.</summary>
    private const byte Unlisted = 0xFE;

    /// <summary>Each glyph's characters.</summary>
    private readonly GlyphCharacters[] _map;

    /// <summary>The glyph indices of the block being built.</summary>
    private readonly List<int> _glyphs = [];

    /// <summary>Initializes a new instance of the <see cref="TaggedBlockWalker"/> class.</summary>
    /// <param name="map">Each glyph's characters.</param>
    internal TaggedBlockWalker(GlyphCharacters[] map) => _map = map;

    /// <summary>Gets the blocks, in logical order.</summary>
    internal List<TaggedBlock> Blocks { get; } = [];

    /// <summary>Gets how many characters the blocks cover.</summary>
    internal int Covered { get; private set; }

    /// <summary>
    /// Gets each structure type's reading kind, indexed by <see cref="PdfStructureType"/>, as PDFium's structure roles
    /// map them: <see cref="Grouping"/>, <see cref="Unlisted"/>, or a <see cref="ReadingBlockKind"/> value.
    /// </summary>
    private static ReadOnlySpan<byte> Kinds =>
    [
        0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0x00, 0x03, 0xFF, 0x00, 0xFF, 0x00,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0xFF, 0x02, 0x02, 0x02, 0xFF, 0xFF, 0x05, 0x05,
        0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x04, 0x04, 0x00,
        0xFE, 0x00, 0x00, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFE, 0xFE, 0xFE, 0x06, 0x06, 0xFF, 0x00, 0x00,
        0x00, 0xFE,
    ];

    /// <summary>Visits a node: a grouping one is walked into, any other becomes a block.</summary>
    /// <param name="node">The node.</param>
    internal void Visit(PdfSemanticNode node)
    {
        if (node.Role == PdfSemanticRole.Content)
        {
            return;
        }

        var type = node.Element?.Type ?? PdfStructureType.Unknown;
        var code = (uint)type < (uint)Kinds.Length ? Kinds[(int)type] : Unlisted;
        if (code == Grouping || (code == Unlisted && HasElementChildren(node)))
        {
            foreach (var child in node.Children)
            {
                Visit(child);
            }

            return;
        }

        var kind = code == Unlisted ? ReadingBlockKind.Paragraph : (ReadingBlockKind)code;
        AddBlock(node, kind);
    }

    /// <summary>Determines whether a node has child elements rather than only content.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasElementChildren(PdfSemanticNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Role != PdfSemanticRole.Content)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Makes a block from a node and everything inside it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="kind">What it reads as.</param>
    private void AddBlock(PdfSemanticNode node, ReadingBlockKind kind)
    {
        _glyphs.Clear();
        node.CollectItems(_glyphs);
        var characters = new List<int>(_glyphs.Count);
        foreach (var glyph in _glyphs)
        {
            TaggedCharacterMap.Append(_map, glyph, characters);
        }

        var replacement = node.ActualText ?? (kind == ReadingBlockKind.Figure ? node.AlternateText : null);
        if (characters.Count == 0 && replacement is null)
        {
            return;
        }

        Covered += characters.Count;
        Blocks.Add(new(kind, node.Element?.HeadingLevel ?? 0, [.. characters], replacement));
    }
}
