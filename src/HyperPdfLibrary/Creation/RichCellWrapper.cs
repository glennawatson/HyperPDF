// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>Measures and wraps rich table cell content (styled text runs, plain text, inline images, code blocks and nested flow blocks).</summary>
internal static class RichCellWrapper
{
    /// <summary>The factor from pixels at 96 DPI to PDF points.</summary>
    private const float PointsPerPixel = 0.75F;

    /// <summary>The two sides of a box that have padding along one axis.</summary>
    private const float PaddingSides = 2F;

    /// <summary>The deepest heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>The size of a level 1 heading as a multiple of the cell size.</summary>
    private const float LevelOneScale = 1.9F;

    /// <summary>The size of a level 2 heading as a multiple of the cell size.</summary>
    private const float LevelTwoScale = 1.55F;

    /// <summary>The size of a level 3 heading as a multiple of the cell size.</summary>
    private const float LevelThreeScale = 1.3F;

    /// <summary>The size of a level 4 heading as a multiple of the cell size.</summary>
    private const float LevelFourScale = 1.15F;

    /// <summary>The size of a level 5 heading as a multiple of the cell size.</summary>
    private const float LevelFiveScale = 1.05F;

    /// <summary>Gets the heading scale for each heading level.</summary>
    private static ReadOnlySpan<float> HeadingScales => [LevelOneScale, LevelTwoScale, LevelThreeScale, LevelFourScale, LevelFiveScale, 1F];

    /// <summary>Measures the natural (unwrapped) content width a cell wants, without cell padding.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="defaultFont">The row's default font.</param>
    /// <param name="defaultSize">The table's default font size.</param>
    /// <param name="defaultColor">The table's default text colour.</param>
    /// <param name="lineSpacing">The flow's line spacing multiplier.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <returns>The widest unwrapped item in points.</returns>
    internal static float MeasureNaturalWidth(
        PdfTableCell cell,
        PdfCanvasFont defaultFont,
        float defaultSize,
        PdfRgbColor defaultColor,
        float lineSpacing,
        ref PooledLines lines)
    {
        var font = cell.Font ?? defaultFont;
        var size = cell.FontSize ?? defaultSize;
        var color = cell.TextColor ?? defaultColor;
        var widest = 0F;
        if (cell.Image is { } image)
        {
            widest = Math.Max(widest, GetImageDesiredWidth(image));
        }

        if (cell.Runs.Count > 0)
        {
            widest = Math.Max(widest, MeasureRunsNatural(cell.Runs, font, size, color, lineSpacing));
        }

        if (!string.IsNullOrEmpty(cell.Text))
        {
            lines.Clear();
            PdfTextWrapper.Wrap(font, size, cell.Text, float.MaxValue, ref lines);
            widest = Math.Max(widest, PdfTableWidths.WidestLine(ref lines));
        }

        if (cell.CodeBlock is { } code)
        {
            widest = Math.Max(widest, MeasureCodeNatural(code, font, ref lines));
        }

        for (var i = 0; i < cell.Blocks.Count; i++)
        {
            widest = Math.Max(widest, MeasureBlockNatural(cell.Blocks[i], font, size, ref lines));
        }

        return widest;
    }

    /// <summary>Wraps all content of a placed cell into its <see cref="PlacedCell.Items"/> list.</summary>
    /// <param name="placed">The placed cell to populate.</param>
    /// <param name="style">The resolved style defaults and dimensions for the cell.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    internal static void WrapCell(
        PlacedCell placed,
        in CellStyleContext style,
        ref PooledLines lines)
    {
        var cell = placed.Cell;
        var items = placed.Items;
        items.Clear();
        if (cell.Image is { } image)
        {
            AddImageItem(image, style.InnerWidth, style.MaxImageHeight, items);
        }

        if (cell.Runs.Count > 0)
        {
            WrapStyledRuns(cell.Runs, in style, items);
        }

        if (!string.IsNullOrEmpty(cell.Text))
        {
            WrapPlainText(cell.Text, style.Font, style.Size, in style, ref lines, items);
        }

        if (cell.CodeBlock is { } code)
        {
            WrapCodeBlock(code, style.Font, style.InnerWidth, ref lines, items);
        }

        for (var i = 0; i < cell.Blocks.Count; i++)
        {
            WrapFlowBlock(cell.Blocks[i], in style, ref lines, items);
        }
    }

    /// <summary>Measures the natural width of styled text runs.</summary>
    /// <param name="runs">The styled runs.</param>
    /// <param name="defaultFont">The cell's font.</param>
    /// <param name="defaultSize">The cell's font size.</param>
    /// <param name="defaultColor">The cell's text colour.</param>
    /// <param name="lineSpacing">The line spacing multiplier.</param>
    /// <returns>The widest unwrapped line in points.</returns>
    private static float MeasureRunsNatural(
        IReadOnlyList<PdfTextRun> runs,
        PdfCanvasFont defaultFont,
        float defaultSize,
        PdfRgbColor defaultColor,
        float lineSpacing)
    {
        var items = new List<CellVisualItem>();
        var style = new CellStyleContext(defaultFont, defaultSize, defaultColor, lineSpacing, float.MaxValue, float.MaxValue);
        WrapStyledRuns(runs, in style, items);
        var widest = 0F;
        for (var i = 0; i < items.Count; i++)
        {
            widest = Math.Max(widest, items[i].Width);
        }

        return widest;
    }

    /// <summary>Measures the natural width of a code block.</summary>
    /// <param name="code">The code block.</param>
    /// <param name="defaultFont">The fallback font.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <returns>The widest unwrapped code line plus code indent and padding.</returns>
    private static float MeasureCodeNatural(PdfFlowCodeBlock code, PdfCanvasFont defaultFont, ref PooledLines lines)
    {
        var font = code.Font ?? defaultFont;
        var expanded = PdfCodeFlow.ExpandTabs(code.Code, code.TabSpaces);
        lines.Clear();
        PdfCodeFlow.WrapCode(font, code.FontSize, expanded, float.MaxValue, PdfCodeOverflow.Wrap, ref lines);
        return PdfTableWidths.WidestLine(ref lines) + code.Indent + (code.Padding * PaddingSides);
    }

    /// <summary>Measures the natural width of a nested flow block.</summary>
    /// <param name="block">The flow block.</param>
    /// <param name="defaultFont">The fallback font.</param>
    /// <param name="defaultSize">The fallback font size.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <returns>The natural width in points.</returns>
    private static float MeasureBlockNatural(PdfFlowBlock block, PdfCanvasFont defaultFont, float defaultSize, ref PooledLines lines)
    {
        switch (block)
        {
            case PdfFlowImage image:
            {
                return GetImageDesiredWidth(image);
            }

            case PdfFlowCodeBlock code:
            {
                return MeasureCodeNatural(code, defaultFont, ref lines);
            }

            case PdfFlowParagraph paragraph:
            {
                lines.Clear();
                PdfTextWrapper.Wrap(defaultFont, defaultSize, paragraph.Text, float.MaxValue, ref lines);
                return PdfTableWidths.WidestLine(ref lines);
            }

            case PdfFlowHeading heading:
            {
                var scale = HeadingScales[Math.Clamp(heading.Level, 1, MaxHeadingLevel) - 1];
                lines.Clear();
                PdfTextWrapper.Wrap(defaultFont, defaultSize * scale, heading.Text, float.MaxValue, ref lines);
                return PdfTableWidths.WidestLine(ref lines);
            }

            case PdfFlowRule rule:
            {
                return rule.Width ?? 0F;
            }

            default:
            {
                return 0F;
            }
        }
    }

    /// <summary>Gets the requested width of an image in points.</summary>
    /// <param name="image">The flow image.</param>
    /// <returns>The requested width in points.</returns>
    private static float GetImageDesiredWidth(PdfFlowImage image) =>
        image.WidthPoints > 0F ? image.WidthPoints : image.Image.Width * PointsPerPixel;

    /// <summary>Adds a scaled or clipped image item to a cell's item list according to <see cref="PdfFlowImage.Fit"/>.</summary>
    /// <param name="block">The image block.</param>
    /// <param name="innerWidth">The cell's available inner width.</param>
    /// <param name="maxImageHeight">The tallest an image may be on a single page.</param>
    /// <param name="items">Receives the image item.</param>
    private static void AddImageItem(PdfFlowImage block, float innerWidth, float maxImageHeight, List<CellVisualItem> items)
    {
        var desired = GetImageDesiredWidth(block);
        if (block.Fit == PdfFlowImageFit.ClipOverflow)
        {
            var rawHeight = desired * block.Image.AspectRatio;
            var boxWidth = Math.Min(desired, innerWidth);
            var boxHeight = maxImageHeight > 0F ? Math.Min(rawHeight, maxImageHeight) : rawHeight;
            items.Add(CellVisualItem.CreateImage(block, boxWidth, boxHeight, desired, rawHeight));
            return;
        }

        var width = Math.Min(desired, innerWidth);
        var height = width * block.Image.AspectRatio;
        if (block.Fit == PdfFlowImageFit.ScaleToWidth)
        {
            var boxHeight = maxImageHeight > 0F ? Math.Min(height, maxImageHeight) : height;
            items.Add(CellVisualItem.CreateImage(block, width, boxHeight, width, height));
            return;
        }

        if (height > maxImageHeight && maxImageHeight > 0F)
        {
            height = maxImageHeight;
            width = height / block.Image.AspectRatio;
        }

        items.Add(CellVisualItem.CreateImage(block, width, height, width, height));
    }

    /// <summary>Wraps plain text into visual text line items.</summary>
    /// <param name="text">The plain text.</param>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="style">The cell style context.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <param name="items">Receives the wrapped line items.</param>
    private static void WrapPlainText(
        string text,
        PdfCanvasFont font,
        float size,
        in CellStyleContext style,
        ref PooledLines lines,
        List<CellVisualItem> items)
    {
        lines.Clear();
        PdfTextWrapper.Wrap(font, size, text, style.InnerWidth, ref lines);
        var leading = size * style.LineSpacing;
        var ascent = font.GetAscent(size);
        var descent = font.GetDescent(size);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            StyledSegment[] segments = line.Length > 0 ? [new(text, line.Start, line.Length, line.Width, font, size, style.Color)] : [];
            items.Add(CellVisualItem.CreateText(segments, line.Width, leading, ascent, descent));
        }
    }

    /// <summary>Wraps a code block into visual code line items inside a cell.</summary>
    /// <param name="block">The code block.</param>
    /// <param name="defaultFont">The fallback font.</param>
    /// <param name="innerWidth">The cell's available inner width.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <param name="items">Receives the code line items.</param>
    private static void WrapCodeBlock(
        PdfFlowCodeBlock block,
        PdfCanvasFont defaultFont,
        float innerWidth,
        ref PooledLines lines,
        List<CellVisualItem> items)
    {
        var font = block.Font ?? defaultFont;
        var expanded = PdfCodeFlow.ExpandTabs(block.Code, block.TabSpaces);
        var boxWidth = Math.Max(1F, innerWidth - block.Indent);
        var codeInnerWidth = Math.Max(1F, boxWidth - (block.Padding * PaddingSides));
        lines.Clear();
        PdfCodeFlow.WrapCode(font, block.FontSize, expanded, codeInnerWidth, block.Overflow, ref lines);
        var leading = block.FontSize * block.LineSpacing;
        var total = Math.Max(lines.Count, 1);
        for (var i = 0; i < total; i++)
        {
            var line = i < lines.Count ? lines[i] : new PdfTextLine(0, 0, 0F);
            var extraPadding = (i == 0 ? block.Padding : 0F) + (i == total - 1 ? block.Padding : 0F);
            items.Add(CellVisualItem.CreateCode(block, font, expanded, line, boxWidth, leading, leading + extraPadding));
        }
    }

    /// <summary>Wraps a flow block inside a table cell.</summary>
    /// <param name="block">The flow block.</param>
    /// <param name="style">The cell style context.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    /// <param name="items">Receives the visual items.</param>
    private static void WrapFlowBlock(
        PdfFlowBlock block,
        in CellStyleContext style,
        ref PooledLines lines,
        List<CellVisualItem> items)
    {
        switch (block)
        {
            case PdfFlowImage image:
            {
                AddImageItem(image, style.InnerWidth, style.MaxImageHeight, items);
                break;
            }

            case PdfFlowCodeBlock code:
            {
                WrapCodeBlock(code, style.Font, style.InnerWidth, ref lines, items);
                break;
            }

            case PdfFlowParagraph paragraph:
            {
                WrapPlainText(paragraph.Text, style.Font, style.Size, in style, ref lines, items);
                break;
            }

            case PdfFlowHeading heading:
            {
                var scale = HeadingScales[Math.Clamp(heading.Level, 1, MaxHeadingLevel) - 1];
                WrapPlainText(heading.Text, style.Font, style.Size * scale, in style, ref lines, items);
                break;
            }

            case PdfFlowRule rule:
            {
                var ruleWidth = rule.Width is { } requested ? Math.Min(requested, style.InnerWidth) : style.InnerWidth;
                var ruleHeight = rule.Thickness + (rule.SpaceBefore ?? 0F) + (rule.SpaceAfter ?? 0F);
                items.Add(CellVisualItem.CreateRule(rule, ruleWidth, ruleHeight));
                break;
            }
        }
    }

    /// <summary>Wraps multiple styled text runs into inline-flowing visual text lines.</summary>
    /// <param name="runs">The styled runs.</param>
    /// <param name="style">The cell style context.</param>
    /// <param name="items">Receives the wrapped text line items.</param>
    private static void WrapStyledRuns(
        IReadOnlyList<PdfTextRun> runs,
        in CellStyleContext style,
        List<CellVisualItem> items)
    {
        var builder = new RichLineAccumulator(style.Font, style.Size, style.LineSpacing, items);
        for (var r = 0; r < runs.Count; r++)
        {
            var run = runs[r];
            builder.AppendRun(run.Text, run.Font ?? style.Font, run.FontSize ?? style.Size, run.Color ?? style.Color, style.InnerWidth);
        }

        builder.Finish();
    }
}
