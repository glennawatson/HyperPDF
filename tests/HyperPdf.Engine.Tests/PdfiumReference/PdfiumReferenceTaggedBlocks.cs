// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Core.Reading;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>
/// Reads a tagged page the way the PDFium engine did before it was removed: the structure tree's block-level elements in
/// logical order, each with the page text characters that PDFium draws in the element's marked content. The tests compare
/// HyperPDF's managed reader with it.
/// </summary>
internal static unsafe partial class PdfiumReferenceTaggedBlocks
{
    /// <summary>The least share of the page's marked text the tags must cover to be trusted over the layout.</summary>
    private const double MinimumCoverage = 0.6;

    /// <summary>The least share of the page's text that must be marked content for the tags to be used.</summary>
    private const double MinimumMarked = 0.5;

    /// <summary>The deepest structure nesting followed, guarding against loops in damaged files.</summary>
    private const int MaxDepth = 64;

    /// <summary>The deepest heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>The length of a two-character heading type such as <c>H2</c>.</summary>
    private const int NumberedHeadingLength = 2;

    /// <summary>Types that only group other elements.</summary>
    private static readonly HashSet<string> Groupings =
    [
        with(StringComparer.Ordinal),
        "Document", "DocumentFragment", "Part", "Art", "Sect", "Div", "Aside", "L", "Table", "TR", "THead", "TBody", "TFoot", "TOC", "Index", "NonStruct", "Private", "Form",
    ];

    /// <summary>Block types and what they read as.</summary>
    private static readonly Dictionary<string, ReadingBlockKind> BlockKinds = new(StringComparer.Ordinal)
    {
        ["P"] = ReadingBlockKind.Paragraph,
        ["BlockQuote"] = ReadingBlockKind.Paragraph,
        ["Quote"] = ReadingBlockKind.Paragraph,
        ["Code"] = ReadingBlockKind.Paragraph,
        ["TOCI"] = ReadingBlockKind.Paragraph,
        ["Span"] = ReadingBlockKind.Paragraph,
        ["Link"] = ReadingBlockKind.Paragraph,
        ["Reference"] = ReadingBlockKind.Paragraph,
        ["Annot"] = ReadingBlockKind.Paragraph,
        ["Sub"] = ReadingBlockKind.Paragraph,
        ["Em"] = ReadingBlockKind.Paragraph,
        ["Strong"] = ReadingBlockKind.Paragraph,
        ["LI"] = ReadingBlockKind.ListItem,
        ["Lbl"] = ReadingBlockKind.ListItem,
        ["LBody"] = ReadingBlockKind.ListItem,
        ["TD"] = ReadingBlockKind.TableCell,
        ["TH"] = ReadingBlockKind.TableCell,
        ["Caption"] = ReadingBlockKind.Caption,
        ["Note"] = ReadingBlockKind.Footnote,
        ["FENote"] = ReadingBlockKind.Footnote,
        ["Figure"] = ReadingBlockKind.Figure,
        ["Formula"] = ReadingBlockKind.Figure,
    };

    /// <summary>Reads a page's tagged blocks.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The page; <see cref="PdfiumTaggedPage.IsTrusted"/> is <see langword="false"/> when the document is untagged or its tags do not cover the page's text well enough.</returns>
    /// <exception cref="InvalidDataException">PDFium cannot load the page.</exception>
    internal static PdfiumTaggedPage GetPage(PdfiumReferenceDocument document, int pageIndex)
    {
        using (PdfiumReferenceLibrary.EnterScope())
        {
            using var page = PdfiumPage.Load(document, pageIndex);
            var textPage = PdfiumReferenceText.NativeMethods.FPDFText_LoadPage(page.Handle);
            if (textPage == 0)
            {
                throw new InvalidDataException("PDFium could not load the page text.");
            }

            try
            {
                var unicodes = ReadUnicodes(textPage);
                var untrusted = new PdfiumTaggedPage(false, [], unicodes);
                if (PdfiumReferenceStructure.NativeMethods.FPDFCatalog_IsTagged(document.Handle) == 0)
                {
                    return untrusted;
                }

                var marked = MapMarkedContent(textPage, unicodes, out var markedCount, out var visibleCount);
                return markedCount == 0 || markedCount < visibleCount * MinimumMarked ? untrusted : ReadTree(page, marked, markedCount, unicodes);
            }
            finally
            {
                PdfiumReferenceText.NativeMethods.FPDFText_ClosePage(textPage);
            }
        }
    }

    /// <summary>Reads every character's Unicode value.</summary>
    /// <param name="textPage">The text page.</param>
    /// <returns>The values by character index.</returns>
    private static uint[] ReadUnicodes(nint textPage)
    {
        var count = Math.Max(PdfiumReferenceText.NativeMethods.FPDFText_CountChars(textPage), 0);
        var unicodes = new uint[count];
        for (var i = 0; i < count; i++)
        {
            unicodes[i] = PdfiumReferenceText.NativeMethods.FPDFText_GetUnicode(textPage, i);
        }

        return unicodes;
    }

    /// <summary>Groups a page's characters by the marked content they are drawn in.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="unicodes">The characters' values.</param>
    /// <param name="markedCount">Receives how many visible characters are in marked content.</param>
    /// <param name="visibleCount">Receives how many characters are visible (not spaces).</param>
    /// <returns>Each marked content id's characters, in drawing order.</returns>
    private static Dictionary<int, List<int>> MapMarkedContent(nint textPage, uint[] unicodes, out int markedCount, out int visibleCount)
    {
        var map = new Dictionary<int, List<int>>();
        markedCount = 0;
        visibleCount = 0;
        for (var i = 0; i < unicodes.Length; i++)
        {
            var textObject = NativeMethods.FPDFText_GetTextObject(textPage, i);
            if (textObject == 0)
            {
                continue;
            }

            var visible = !char.IsWhiteSpace((char)unicodes[i]);
            visibleCount += visible ? 1 : 0;
            var id = NativeMethods.FPDFPageObj_GetMarkedContentID(textObject);
            if (id < 0)
            {
                continue;
            }

            markedCount += visible ? 1 : 0;
            ref var characters = ref CollectionsMarshal.GetValueRefOrAddDefault(map, id, out _);
            characters ??= [];
            characters.Add(i);
        }

        return map;
    }

    /// <summary>Walks a page's structure tree into blocks.</summary>
    /// <param name="page">The page.</param>
    /// <param name="marked">Each marked content id's characters.</param>
    /// <param name="markedCount">How many visible characters are in marked content.</param>
    /// <param name="unicodes">The characters' values.</param>
    /// <returns>The tagged page.</returns>
    private static PdfiumTaggedPage ReadTree(PdfiumPage page, Dictionary<int, List<int>> marked, int markedCount, uint[] unicodes)
    {
        var tree = PdfiumReferenceStructure.NativeMethods.FPDF_StructTree_GetForPage(page.Handle);
        var untrusted = new PdfiumTaggedPage(false, [], unicodes);
        if (tree == 0)
        {
            return untrusted;
        }

        try
        {
            var walker = new StructureWalker(marked);
            var count = PdfiumReferenceStructure.NativeMethods.FPDF_StructTree_CountChildren(tree);
            for (var i = 0; i < count; i++)
            {
                walker.Visit(PdfiumReferenceStructure.NativeMethods.FPDF_StructTree_GetChildAtIndex(tree, i), 0);
            }

            return walker.Covered < markedCount * MinimumCoverage || walker.Blocks.Count == 0 ? untrusted : new(true, [.. walker.Blocks], unicodes);
        }
        finally
        {
            PdfiumReferenceStructure.NativeMethods.FPDF_StructTree_Close(tree);
        }
    }

    /// <summary>Reads an element's type after role mapping.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The type, or an empty string.</returns>
    private static string ReadType(nint element) =>
        PdfiumNativeStrings.ReadUtf16(element, &PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetType) ?? string.Empty;

    /// <summary>PDFium's marked content functions for text and structure elements.</summary>
    internal static partial class NativeMethods
    {
        /// <summary>The native library name.</summary>
        private const string Library = "pdfium";

        /// <summary>Gets the text object a character is drawn by.</summary>
        /// <param name="textPage">The text page.</param>
        /// <param name="index">The character.</param>
        /// <returns>The page object, or zero.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint FPDFText_GetTextObject(nint textPage, int index);

        /// <summary>Gets the marked content id of a page object.</summary>
        /// <param name="pageObject">The page object.</param>
        /// <returns>The id, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDFPageObj_GetMarkedContentID(nint pageObject);

        /// <summary>Gets the marked content id of a child that is not an element.</summary>
        /// <param name="element">The element.</param>
        /// <param name="index">The child.</param>
        /// <returns>The id, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructElement_GetChildMarkedContentID(nint element, int index);

        /// <summary>Counts the marked content ids an element holds directly.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The count, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructElement_GetMarkedContentIdCount(nint element);

        /// <summary>Gets one of the marked content ids an element holds directly.</summary>
        /// <param name="element">The element.</param>
        /// <param name="index">The id's position.</param>
        /// <returns>The id, or -1.</returns>
        [LibraryImport(Library)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial int FPDF_StructElement_GetMarkedContentIdAtIndex(nint element, int index);
    }

    /// <summary>Walks a page's structure tree, turning each block-level element into a <see cref="TaggedBlock"/>.</summary>
    /// <param name="marked">Each marked content id's characters.</param>
    internal sealed class StructureWalker(Dictionary<int, List<int>> marked)
    {
        /// <summary>The marked content ids already given to a block, so shared content is read once.</summary>
        private readonly HashSet<int> _used = [];

        /// <summary>Gets the blocks found, in logical order.</summary>
        internal List<TaggedBlock> Blocks { get; } = [];

        /// <summary>Gets how many characters the blocks cover.</summary>
        internal int Covered { get; private set; }

        /// <summary>Visits an element: a block-level one becomes a block; a grouping one is walked into.</summary>
        /// <param name="element">The element.</param>
        /// <param name="depth">How deep it is.</param>
        internal void Visit(nint element, int depth)
        {
            if (element == 0 || depth > MaxDepth)
            {
                return;
            }

            var type = ReadType(element);
            var grouping = Groupings.Contains(type);
            var unlisted = !grouping && !IsHeading(type, out _) && !BlockKinds.ContainsKey(type);
            if (grouping || (unlisted && HasElementChildren(element)))
            {
                var count = PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_CountChildren(element);
                for (var i = 0; i < count; i++)
                {
                    Visit(PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i), depth + 1);
                }

                return;
            }

            AddBlock(element, type);
        }

        /// <summary>Determines whether a type is a heading, and which level.</summary>
        /// <param name="type">The type.</param>
        /// <param name="level">Receives the level.</param>
        /// <returns><see langword="true"/> for a heading.</returns>
        private static bool IsHeading(string type, out int level)
        {
            level = 0;
            if (type is "Title" or "H")
            {
                level = 1;
                return true;
            }

            if (type.Length == NumberedHeadingLength && type[0] == 'H' && type[1] is >= '1' and <= '9')
            {
                level = Math.Min(type[1] - '0', MaxHeadingLevel);
                return true;
            }

            return false;
        }

        /// <summary>Determines whether an element has child elements rather than only marked content.</summary>
        /// <param name="element">The element.</param>
        /// <returns><see langword="true"/> when it has.</returns>
        private static bool HasElementChildren(nint element)
        {
            var count = PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_CountChildren(element);
            for (var i = 0; i < count; i++)
            {
                if (PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Makes a block from an element and everything inside it.</summary>
        /// <param name="element">The element.</param>
        /// <param name="type">The element's type.</param>
        private void AddBlock(nint element, string type)
        {
            var kind = IsHeading(type, out var level) ? ReadingBlockKind.Heading : BlockKinds.GetValueOrDefault(type, ReadingBlockKind.Paragraph);
            var characters = new List<int>();
            Collect(element, characters, 0);
            var replacement = PdfiumNativeStrings.ReadUtf16(element, &PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetActualText);
            if (kind == ReadingBlockKind.Figure && replacement is null)
            {
                replacement = PdfiumNativeStrings.ReadUtf16(element, &PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetAltText);
            }

            if (characters.Count == 0 && replacement is null)
            {
                return;
            }

            Covered += characters.Count;
            Blocks.Add(new(kind, level, [.. characters], replacement));
        }

        /// <summary>Gathers the characters of an element's marked content and of its descendants, in order.</summary>
        /// <param name="element">The element.</param>
        /// <param name="characters">Receives the character indices.</param>
        /// <param name="depth">How deep it is.</param>
        private void Collect(nint element, List<int> characters, int depth)
        {
            if (depth > MaxDepth)
            {
                return;
            }

            var count = PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_CountChildren(element);
            for (var i = 0; i < count; i++)
            {
                var child = PdfiumReferenceStructure.NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i);
                if (child != 0)
                {
                    Collect(child, characters, depth + 1);
                    continue;
                }

                Take(NativeMethods.FPDF_StructElement_GetChildMarkedContentID(element, i), characters);
            }

            var direct = NativeMethods.FPDF_StructElement_GetMarkedContentIdCount(element);
            for (var i = 0; i < direct; i++)
            {
                Take(NativeMethods.FPDF_StructElement_GetMarkedContentIdAtIndex(element, i), characters);
            }
        }

        /// <summary>Adds a marked content id's characters, once.</summary>
        /// <param name="id">The id.</param>
        /// <param name="characters">Receives the character indices.</param>
        private void Take(int id, List<int> characters)
        {
            if (id >= 0 && _used.Add(id) && marked.TryGetValue(id, out var drawn))
            {
                characters.AddRange(drawn);
            }
        }
    }
}
