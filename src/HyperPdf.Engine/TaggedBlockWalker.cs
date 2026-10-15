// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>
/// Turns a page's tagged reading nodes into <see cref="TaggedBlock"/> values: grouping
/// elements are walked into, every other element becomes one block of the characters of its content and its
/// descendants', and an element with no characters and no replacement text is dropped.
/// </summary>
[DebuggerDisplay("TaggedBlockWalker: {Blocks.Count} blocks")]
internal sealed class TaggedBlockWalker
{
    /// <summary>The code of a grouping type in <see cref="Codes"/>.</summary>
    private const byte Grouping = 0xFF;

    /// <summary>The code of a type with no reading kind of its own, read as a paragraph unless it has child elements.</summary>
    private const byte Unlisted = 0xFE;

    /// <summary>The number of structure types.</summary>
    private const int TypeCount = (int)PdfStructureType.Artifact + 1;

    /// <summary>
    /// How each structure type reads (ISO 32000-2, 14.8.4), indexed by <see cref="PdfStructureType"/>: grouping elements
    /// (document parts, sections, lists, tables, forms) are walked into, <see cref="Unlisted"/> elements have no reading
    /// kind of their own, and every other element is a block of one <see cref="ReadingBlockKind"/>.
    /// </summary>
    private static readonly byte[] Codes = BuildCodes();

    /// <summary>The blocks list a walker holds between walks; nothing is ever added to it.</summary>
    private static readonly List<TaggedBlock> NoBlocks = [];

    /// <summary>The glyph indices of the block being built.</summary>
    private readonly List<int> _glyphs = [];

    /// <summary>The character indices of the block being built.</summary>
    private readonly List<int> _characters = [];

    /// <summary>Each glyph's characters.</summary>
    private GlyphCharacters[] _map = [];

    /// <summary>Gets the blocks, in logical order.</summary>
    internal List<TaggedBlock> Blocks { get; private set; } = NoBlocks;

    /// <summary>Gets how many characters the blocks cover.</summary>
    internal int Covered { get; private set; }

    /// <summary>Starts a walk, so one walker and its index buffers serve every page a thread reads.</summary>
    /// <param name="map">Each glyph's characters.</param>
    /// <param name="blocks">Receives the blocks, in logical order.</param>
    internal void Begin(GlyphCharacters[] map, List<TaggedBlock> blocks)
    {
        _map = map;
        Blocks = blocks;
        Covered = 0;
    }

    /// <summary>Ends a walk, letting go of the page's map and the output list.</summary>
    internal void End()
    {
        _map = [];
        Blocks = NoBlocks;
        _glyphs.Clear();
        _characters.Clear();
    }

    /// <summary>Visits a node: a grouping one is walked into, any other becomes a block.</summary>
    /// <param name="node">The node.</param>
    internal void Visit(PdfSemanticNode node)
    {
        if (node.Role == PdfSemanticRole.Content)
        {
            return;
        }

        var type = (int)(node.Element?.Type ?? PdfStructureType.Unknown);
        var code = (uint)type < (uint)Codes.Length ? Codes[type] : Unlisted;
        if (code == Grouping || (code == Unlisted && HasElementChildren(node)))
        {
            // An indexed walk reads the children without making an enumerator for each node.
            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                Visit(children[i]);
            }

            return;
        }

        var kind = code == Unlisted ? ReadingBlockKind.Paragraph : (ReadingBlockKind)code;
        AddBlock(node, kind);
    }

    /// <summary>Builds the reading code of every structure type.</summary>
    /// <returns>The codes.</returns>
    private static byte[] BuildCodes()
    {
        var codes = new byte[TypeCount];
        Array.Fill(codes, Unlisted);
        Assign(codes, Grouping, PdfStructureType.Document, PdfStructureType.DocumentFragment, PdfStructureType.Part, PdfStructureType.Article, PdfStructureType.Section);
        Assign(codes, Grouping, PdfStructureType.Division, PdfStructureType.Aside, PdfStructureType.NonStructural, PdfStructureType.Private, PdfStructureType.Form);
        Assign(codes, Grouping, PdfStructureType.TableOfContents, PdfStructureType.Index, PdfStructureType.List, PdfStructureType.Table);
        Assign(codes, Grouping, PdfStructureType.TableRow, PdfStructureType.TableHead, PdfStructureType.TableBody, PdfStructureType.TableFoot);
        Assign(codes, (byte)ReadingBlockKind.Heading, PdfStructureType.Heading, PdfStructureType.Heading1, PdfStructureType.Heading2, PdfStructureType.Heading3);
        Assign(codes, (byte)ReadingBlockKind.Heading, PdfStructureType.Heading4, PdfStructureType.Heading5, PdfStructureType.Heading6, PdfStructureType.Title);
        Assign(codes, (byte)ReadingBlockKind.ListItem, PdfStructureType.ListItem, PdfStructureType.Label, PdfStructureType.ListBody);
        Assign(codes, (byte)ReadingBlockKind.Caption, PdfStructureType.Caption);
        Assign(codes, (byte)ReadingBlockKind.TableCell, PdfStructureType.TableHeaderCell, PdfStructureType.TableDataCell);
        Assign(codes, (byte)ReadingBlockKind.Footnote, PdfStructureType.Note, PdfStructureType.FootnoteOrEndnote);
        Assign(codes, (byte)ReadingBlockKind.Figure, PdfStructureType.Figure, PdfStructureType.Formula);
        Assign(codes, (byte)ReadingBlockKind.Paragraph, PdfStructureType.BlockQuote, PdfStructureType.TableOfContentsItem, PdfStructureType.Paragraph);
        Assign(codes, (byte)ReadingBlockKind.Paragraph, PdfStructureType.Span, PdfStructureType.Quote, PdfStructureType.Reference, PdfStructureType.Code);
        Assign(codes, (byte)ReadingBlockKind.Paragraph, PdfStructureType.Link, PdfStructureType.Annotation, PdfStructureType.Emphasis, PdfStructureType.Strong);
        Assign(codes, (byte)ReadingBlockKind.Paragraph, PdfStructureType.Subdivision);
        return codes;
    }

    /// <summary>Gives several structure types one code.</summary>
    /// <param name="codes">The codes.</param>
    /// <param name="code">The code.</param>
    /// <param name="types">The structure types.</param>
    private static void Assign(byte[] codes, byte code, params ReadOnlySpan<PdfStructureType> types)
    {
        foreach (var type in types)
        {
            codes[(int)type] = code;
        }
    }

    /// <summary>Determines whether a node has child elements rather than only content.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasElementChildren(PdfSemanticNode node)
    {
        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Role != PdfSemanticRole.Content)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a table cell's row or column as a position counted from 1.</summary>
    /// <param name="kind">What the block reads as.</param>
    /// <param name="index">The node's row or column, from zero, or -1 when it has none.</param>
    /// <returns>The position; zero for a block that is not a table cell.</returns>
    private static int Place(ReadingBlockKind kind, int index) => kind == ReadingBlockKind.TableCell && index >= 0 ? index + 1 : 0;

    /// <summary>Makes a block from a node and everything inside it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="kind">What it reads as.</param>
    private void AddBlock(PdfSemanticNode node, ReadingBlockKind kind)
    {
        _glyphs.Clear();
        node.CollectItems(_glyphs);
        var characters = _characters;
        characters.Clear();
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
        Blocks.Add(new(kind, node.Element?.HeadingLevel ?? 0, [.. characters], replacement) { Row = Place(kind, node.Row), Column = Place(kind, node.Column) });
    }
}
