// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Builds one page's reading nodes from the structure tree: each element with content or an object on the page
/// becomes a node, and each marked content id a content leaf holding its glyphs. A marked content id is read once,
/// by the first element in logical order that claims it, so shared content is not repeated.
/// </summary>
[DebuggerDisplay("TaggedNodeBuilder: page {_pageIndex}")]
internal sealed class TaggedNodeBuilder
{
    /// <summary>The page.</summary>
    private readonly PdfPage _page;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>What the page draws.</summary>
    private readonly PdfMarkedContentPage _content;

    /// <summary>Resolves object references to annotations and form fields.</summary>
    private readonly TaggedObjectResolver _objects;

    /// <summary>The marked content ids already given to a node.</summary>
    private HashSet<int>? _used;

    /// <summary>Initializes a new instance of the <see cref="TaggedNodeBuilder"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    internal TaggedNodeBuilder(PdfDocument document, int pageIndex)
    {
        _pageIndex = pageIndex;
        _page = PdfDocumentPages.GetPage(document, pageIndex);
        _content = PdfDocumentTagged.GetMarkedContent(document, pageIndex);
        _objects = new(document, _page);
    }

    /// <summary>Gets what the page draws.</summary>
    internal PdfMarkedContentPage Content => _content;

    /// <summary>Builds the page's top-level nodes.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="output">Receives the nodes, in logical order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Build(PdfStructureTree tree, List<PdfSemanticNode> output) => Build(tree, output, CancellationToken.None);

    /// <summary>Builds the page's top-level nodes, stopping between elements when cancelled.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="output">Receives the nodes, in logical order.</param>
    /// <param name="cancellationToken">Cancels structure traversal.</param>
    internal void Build(PdfStructureTree tree, List<PdfSemanticNode> output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roots = tree.Roots;
        for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            if (BuildElement(roots[rootIndex], cancellationToken) is { } node)
            {
                output.Add(node);
            }
        }
    }

    /// <summary>Determines whether an element is a table data or header cell.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> for a cell.</returns>
    private static bool IsTableCell(PdfStructureElement element) => element.Type is PdfStructureType.TableDataCell or PdfStructureType.TableHeaderCell;

    /// <summary>Builds an element's node, or nothing when it has nothing on the page.</summary>
    /// <param name="element">The element.</param>
    /// <param name="cancellationToken">Cancels structure traversal.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    private PdfSemanticNode? BuildElement(PdfStructureElement element, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (element.Type == PdfStructureType.Artifact)
        {
            return null;
        }

        var node = new PdfSemanticNode(element.Role, PdfNodeOrigin.Tagged, _pageIndex) { Element = element, Level = element.HeadingLevel, ActualText = element.ActualText };
        if (node.Role == PdfSemanticRole.List)
        {
            node.ListNumbering = ListNumberingReader.Read(element, cancellationToken);
        }

        var kids = element.Kids;
        for (var kidIndex = 0; kidIndex < kids.Count; kidIndex++)
        {
            AddKid(node, kids[kidIndex], cancellationToken);
        }

        if (!Belongs(node, element))
        {
            return null;
        }

        Finish(node);
        return node;
    }

    /// <summary>
    /// Determines whether a node shows on the page: it holds content or an object there, describes something there, or is a
    /// table cell on the page. An empty cell is kept so the cells after it keep their columns and a reader can say it is empty.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="element">Its element.</param>
    /// <returns><see langword="true"/> when the page should read it.</returns>
    private bool Belongs(PdfSemanticNode node, PdfStructureElement element) =>
        node.Children.Count > 0 || node.Annotation is not null
        || (element.PageIndex == _pageIndex && (element.ActualText is not null || element.AlternateText is not null || IsTableCell(element)));

    /// <summary>Adds one kid to an element's node.</summary>
    /// <param name="node">The element's node.</param>
    /// <param name="kid">The kid.</param>
    /// <param name="cancellationToken">Cancels structure traversal.</param>
    private void AddKid(PdfSemanticNode node, in PdfStructureKid kid, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (kid.Kind)
        {
            case PdfStructureKidKind.Element when kid.Element is { } child:
                {
                    if (BuildElement(child, cancellationToken) is { } childNode)
                    {
                        node.Add(childNode);
                    }

                    break;
                }

            case PdfStructureKidKind.MarkedContent when IsOnPage(kid.PageIndex):
                {
                    if (BuildContent(kid.Mcid) is { } leaf)
                    {
                        node.Add(leaf);
                    }

                    break;
                }

            case PdfStructureKidKind.Object:
                {
                    _objects.Attach(node, kid);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Determines whether a kid's page is this page; a kid whose page is unknown counts, so content is not lost when /Pg is missing.</summary>
    /// <param name="pageIndex">The kid's page.</param>
    /// <returns><see langword="true"/> when it is on this page.</returns>
    private bool IsOnPage(int pageIndex) => pageIndex == _pageIndex || pageIndex < 0;

    /// <summary>Makes the content leaf of a marked content id, once.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The leaf, or <see langword="null"/> when the id was read before or draws nothing.</returns>
    private PdfSemanticNode? BuildContent(int mcid)
    {
        if (mcid < 0 || !_content.HasContent(mcid))
        {
            return null;
        }

        _used ??= [];
        if (!_used.Add(mcid))
        {
            return null;
        }

        var leaf = new PdfSemanticNode(PdfSemanticRole.Content, PdfNodeOrigin.Tagged, _pageIndex) { Mcid = mcid, Bounds = _content.GetBounds(mcid) };
        leaf.SetGlyphs(_content, _content.GetGlyphs(mcid).ToArray());
        return leaf;
    }

    /// <summary>Works out an element node's box from its children, sets its text to come from them, and links a table's cells to their headers.</summary>
    /// <param name="node">The node.</param>
    private void Finish(PdfSemanticNode node)
    {
        var bounds = node.Bounds;
        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            bounds = bounds.Union(children[i].Bounds);
        }

        node.JoinChildText();
        node.Bounds = bounds.IsEmpty ? LayoutBounds(node.Element) : bounds;
        if (node.Role == PdfSemanticRole.Table)
        {
            TableHeaderResolver.Resolve(node);
        }
    }

    /// <summary>Gets an element's Layout <c>/BBox</c> in viewer space, for an element that draws nothing else.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The box, or empty.</returns>
    private PdfViewerRect LayoutBounds(PdfStructureElement? element) =>
        element?.Attributes.BoundingBox is { } box && element.PageIndex == _pageIndex
            ? PdfViewerRect.FromViewerRectangle(_page.ToViewerRectangle(box))
            : default;
}
