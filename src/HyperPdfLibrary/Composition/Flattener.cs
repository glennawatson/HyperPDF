// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Charts;

namespace HyperPdfLibrary.Composition;

/// <summary>Turns blocks into flowable items, expanding lists into their paragraphs with labels and hanging indents.</summary>
[DebuggerDisplay("Flattener")]
internal sealed class Flattener
{
    /// <summary>The factor from pixels at 96 to the inch to points.</summary>
    private const float PixelsToPoints = 0.75F;

    /// <summary>The size in points of the text in charts that name no style.</summary>
    private const float ChartFontSize = 9F;

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>Initializes a new instance of the <see cref="Flattener"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    internal Flattener(LayoutEnv env) => _env = env;

    /// <summary>Flattens blocks.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="scope">Where the blocks are.</param>
    /// <returns>The items, in order.</returns>
    /// <exception cref="ArgumentException">A block is not supported.</exception>
    internal List<Flowable> Flatten(IReadOnlyList<PdfBlock> blocks, FlattenScope scope)
    {
        var items = new List<Flowable>();
        foreach (var block in blocks)
        {
            Add(items, block, scope);
        }

        return items;
    }

    /// <summary>Adds the items of one block.</summary>
    /// <param name="items">The items so far.</param>
    /// <param name="block">The block.</param>
    /// <param name="scope">Where the block is.</param>
    /// <exception cref="ArgumentException">The block is not supported.</exception>
    private void Add(List<Flowable> items, PdfBlock block, FlattenScope scope)
    {
        switch (block)
        {
            case PdfParagraph paragraph:
            {
                var styled = paragraph.Style is null && scope.DefaultStyle is { } style ? paragraph with { Style = style } : paragraph;
                items.Add(new ParagraphFlow(_env, styled, new(scope.Indent, 0F, null, null), scope.Parent));
                break;
            }

            case PdfGrid grid:
            {
                items.Add(new GridFlow(_env, grid, scope));
                break;
            }

            case PdfFrame frame:
            {
                items.Add(new FrameFlow(_env, frame, scope));
                break;
            }

            case PdfList list:
            {
                AddList(items, list, scope);
                break;
            }

            case PdfRule rule:
            {
                items.Add(new RuleFlow(rule, scope.Indent));
                break;
            }

            case PdfPageBreak:
            {
                items.Add(new PageBreakFlow());
                break;
            }

            default:
            {
                items.Add(CreateFigure(block, scope));
                break;
            }
        }
    }

    /// <summary>Makes the item for an image, chart or barcode.</summary>
    /// <param name="block">The block.</param>
    /// <param name="scope">Where the block is.</param>
    /// <returns>The item.</returns>
    /// <exception cref="ArgumentException">The block is not supported.</exception>
    private FigureFlow CreateFigure(PdfBlock block, FlattenScope scope) => new(_env, GetFigureSpec(block), scope.Indent, scope.Parent);

    /// <summary>Works out what a figure block needs.</summary>
    /// <param name="block">The block.</param>
    /// <returns>The specification.</returns>
    /// <exception cref="ArgumentException">The block is not an image, chart or barcode.</exception>
    private FigureSpec GetFigureSpec(PdfBlock block) => block switch
    {
        PdfImageBlock image => new(
            image.Width,
            image.Height,
            image.Image.Width * PixelsToPoints,
            image.Image.Height * PixelsToPoints,
            image.Alignment,
            image.SpaceBefore,
            image.SpaceAfter,
            image.AlternativeText,
            (box, node, artifact) => new ImageCommand(node, artifact, image.Image, box)),
        PdfChartBlock chart => new(
            chart.Width,
            chart.Height,
            0F,
            0F,
            chart.Alignment,
            chart.SpaceBefore,
            chart.SpaceAfter,
            chart.AlternativeText ?? chart.Chart.Describe(),
            (box, node, artifact) => new ChartCommand(node, artifact, chart.Chart, chart.Style ?? CreateChartStyle(), box)),
        PdfBarcodeBlock barcode => new(
            barcode.Width,
            barcode.Height,
            0F,
            0F,
            barcode.Alignment,
            barcode.SpaceBefore,
            barcode.SpaceAfter,
            barcode.AlternativeText ?? barcode.Barcode.Text,
            (box, node, artifact) => new BarcodeCommand(node, artifact, barcode.Barcode, barcode.Style ?? PdfBarcodeStyle.Default, box)),
        _ => throw new ArgumentException($"The block '{block}' is not supported.", nameof(block)),
    };

    /// <summary>Makes the style charts use when a chart block names none: the Normal style's typeface at a small size.</summary>
    /// <returns>The style.</returns>
    /// <exception cref="InvalidOperationException">The Normal style has no typeface.</exception>
    private PdfChartStyle CreateChartStyle()
    {
        var family = _env.Styles.Resolve(PdfStyleNames.Normal).Character.Family ?? throw new InvalidOperationException("The Normal style has no typeface.");
        return new(family.Regular, ChartFontSize);
    }

    /// <summary>Adds the items of a list: each list item's first paragraph carries its label, and the rest of its blocks follow, indented.</summary>
    /// <param name="items">The items so far.</param>
    /// <param name="list">The list.</param>
    /// <param name="scope">Where the list is.</param>
    private void AddList(List<Flowable> items, PdfList list, FlattenScope scope)
    {
        var listNode = _env.CreateNode("L", scope.Parent);
        var number = list.Start;
        foreach (var item in list.Items)
        {
            var itemNode = _env.CreateNode("LI", listNode);
            var label = list.Kind == PdfListKind.Bullet ? list.Bullet : NumberText.Format(number, list.NumberFormat) + list.NumberSuffix;
            number++;
            AddListItem(items, list, item, label, new(scope.Indent, itemNode, scope.DefaultStyle));
        }
    }

    /// <summary>Adds the items of one list item.</summary>
    /// <param name="items">The items so far.</param>
    /// <param name="list">The list.</param>
    /// <param name="item">The list item.</param>
    /// <param name="label">The bullet or number.</param>
    /// <param name="scope">Where the list item is, with its structure node as the parent.</param>
    private void AddListItem(List<Flowable> items, PdfList list, PdfListItem item, string label, FlattenScope scope)
    {
        var labelNode = _env.CreateNode("Lbl", scope.Parent);
        var bodyNode = _env.CreateNode("LBody", scope.Parent);
        var style = list.Style ?? (list.Kind == PdfListKind.Bullet ? PdfStyleNames.ListBullet : PdfStyleNames.ListNumber);
        var adjust = new ParagraphAdjust(scope.Indent + list.Indent, list.Indent, label, labelNode);
        var rest = 0;
        if (item.Blocks.Count > 0 && item.Blocks[0] is PdfParagraph first)
        {
            items.Add(new ParagraphFlow(_env, first with { Style = first.Style ?? style }, adjust, bodyNode));
            rest = 1;
        }
        else
        {
            items.Add(new ParagraphFlow(_env, new PdfParagraph([]) { Style = style }, adjust, bodyNode));
        }

        var inner = new FlattenScope(scope.Indent + list.Indent, bodyNode, scope.DefaultStyle);
        for (var i = rest; i < item.Blocks.Count; i++)
        {
            Add(items, item.Blocks[i], inner);
        }
    }
}
