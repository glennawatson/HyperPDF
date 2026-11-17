// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Builds and runs the content-operator dispatch table.</summary>
internal static unsafe class PageContentParseDispatch
{
    /// <summary>The handlers, indexed by <see cref = "ContentOperator"/>; operators the parser ignores point at a no-op.</summary>
    private static readonly delegate*<PageContentParseState, ref ContentReader, void>[] Handlers = PageContentParseDispatch.CreateHandlers();

    /// <summary>Builds the dispatch table.</summary>
    /// <returns>The table.</returns>
    internal static delegate*<PageContentParseState, ref ContentReader, void>[] CreateHandlers()
    {
        var table = new delegate*<PageContentParseState, ref ContentReader, void>[Enum.GetValues<ContentOperator>().Length];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = &PageContentParseDispatch.OpIgnore;
        }

        PageContentParseDispatch.RegisterPaths(table);
        PageContentParseDispatch.RegisterState(table);
        PageContentParseDispatch.RegisterColors(table);
        PageContentParseDispatch.RegisterText(table);
        PageContentParseDispatch.RegisterOther(table);
        return table;
    }

    /// <summary>Registers path construction, painting and clipping.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterPaths(delegate*<PageContentParseState, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.MoveTo] = &PageContentParseGraphics.OpMoveTo;
        table[(int)ContentOperator.LineTo] = &PageContentParseGraphics.OpLineTo;
        table[(int)ContentOperator.CurveTo] = &PageContentParseGraphics.OpCurveTo;
        table[(int)ContentOperator.CurveToV] = &PageContentParseGraphics.OpCurveToV;
        table[(int)ContentOperator.CurveToY] = &PageContentParseGraphics.OpCurveToY;
        table[(int)ContentOperator.ClosePath] = &PageContentParseGraphics.OpClosePath;
        table[(int)ContentOperator.Rectangle] = &PageContentParseGraphics.OpRectangle;
        table[(int)ContentOperator.Fill] = &PageContentParseGraphics.OpFill;
        table[(int)ContentOperator.FillEvenOdd] = &PageContentParseGraphics.OpFillEvenOdd;
        table[(int)ContentOperator.Stroke] = &PageContentParseGraphics.OpStroke;
        table[(int)ContentOperator.CloseStroke] = &PageContentParseGraphics.OpCloseStroke;
        table[(int)ContentOperator.FillStroke] = &PageContentParseGraphics.OpFillStroke;
        table[(int)ContentOperator.FillStrokeEvenOdd] = &PageContentParseGraphics.OpFillStrokeEvenOdd;
        table[(int)ContentOperator.CloseFillStroke] = &PageContentParseGraphics.OpCloseFillStroke;
        table[(int)ContentOperator.CloseFillStrokeEvenOdd] = &PageContentParseGraphics.OpCloseFillStrokeEvenOdd;
        table[(int)ContentOperator.EndPath] = &PageContentParseGraphics.OpEndPath;
        table[(int)ContentOperator.Clip] = &PageContentParseGraphics.OpClip;
        table[(int)ContentOperator.ClipEvenOdd] = &PageContentParseGraphics.OpClipEvenOdd;
    }

    /// <summary>Registers the graphics state operators the parser follows.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterState(delegate*<PageContentParseState, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.Save] = &PageContentParseGraphics.OpSave;
        table[(int)ContentOperator.Restore] = &PageContentParseGraphics.OpRestore;
        table[(int)ContentOperator.ConcatMatrix] = &PageContentParseGraphics.OpConcatMatrix;
        table[(int)ContentOperator.SetLineWidth] = &PageContentParseGraphics.OpSetLineWidth;
        table[(int)ContentOperator.SetGraphicsState] = &PageContentParseGraphics.OpSetGraphicsState;
    }

    /// <summary>Registers the colour operators.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterColors(delegate*<PageContentParseState, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.SetFillGray] = &PageContentParseColors.OpSetFillGray;
        table[(int)ContentOperator.SetStrokeGray] = &PageContentParseColors.OpSetStrokeGray;
        table[(int)ContentOperator.SetFillRgb] = &PageContentParseColors.OpSetFillRgb;
        table[(int)ContentOperator.SetStrokeRgb] = &PageContentParseColors.OpSetStrokeRgb;
        table[(int)ContentOperator.SetFillCmyk] = &PageContentParseColors.OpSetFillCmyk;
        table[(int)ContentOperator.SetStrokeCmyk] = &PageContentParseColors.OpSetStrokeCmyk;
        table[(int)ContentOperator.SetFillColorSpace] = &PageContentParseColors.OpSetFillColorSpace;
        table[(int)ContentOperator.SetStrokeColorSpace] = &PageContentParseColors.OpSetStrokeColorSpace;
        table[(int)ContentOperator.SetFillColor] = &PageContentParseColors.OpSetFillColor;
        table[(int)ContentOperator.SetFillColorN] = &PageContentParseColors.OpSetFillColor;
        table[(int)ContentOperator.SetStrokeColor] = &PageContentParseColors.OpSetStrokeColor;
        table[(int)ContentOperator.SetStrokeColorN] = &PageContentParseColors.OpSetStrokeColor;
    }

    /// <summary>Registers the text operators.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterText(delegate*<PageContentParseState, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.BeginText] = &PageContentParseText.OpBeginText;
        table[(int)ContentOperator.SetCharacterSpacing] = &PageContentParseText.OpSetCharacterSpacing;
        table[(int)ContentOperator.SetWordSpacing] = &PageContentParseText.OpSetWordSpacing;
        table[(int)ContentOperator.SetHorizontalScaling] = &PageContentParseText.OpSetHorizontalScaling;
        table[(int)ContentOperator.SetLeading] = &PageContentParseText.OpSetLeading;
        table[(int)ContentOperator.SetFont] = &PageContentParseText.OpSetFont;
        table[(int)ContentOperator.SetRenderMode] = &PageContentParseText.OpSetRenderMode;
        table[(int)ContentOperator.SetRise] = &PageContentParseText.OpSetRise;
        table[(int)ContentOperator.MoveText] = &PageContentParseText.OpMoveText;
        table[(int)ContentOperator.MoveTextSetLeading] = &PageContentParseText.OpMoveTextSetLeading;
        table[(int)ContentOperator.SetTextMatrix] = &PageContentParseText.OpSetTextMatrix;
        table[(int)ContentOperator.NextLine] = &PageContentParseText.OpNextLine;
        table[(int)ContentOperator.ShowText] = &PageContentParseText.OpShowText;
        table[(int)ContentOperator.ShowTextArray] = &PageContentParseText.OpShowTextArray;
        table[(int)ContentOperator.NextLineShowText] = &PageContentParseText.OpNextLineShowText;
        table[(int)ContentOperator.SetSpacingNextLineShowText] = &PageContentParseText.OpSpacingNextLineShowText;
    }

    /// <summary>Does nothing; the handler for operators that change nothing the parser tracks.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpIgnore(PageContentParseState self, ref ContentReader reader)
    {
        // Flatness, rendering intent, dash, caps, joins, glyph widths and compatibility sections are kept as raw bytes.
    }

    /// <summary>Registers XObjects, inline images, shadings and marked content.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterOther(delegate*<PageContentParseState, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.PaintXObject] = &PageContentParseXObjects.OpPaintXObject;
        table[(int)ContentOperator.BeginInlineImage] = &PageContentParseXObjects.OpInlineImage;
        table[(int)ContentOperator.PaintShading] = &PageContentParseXObjects.OpPaintShading;
        table[(int)ContentOperator.BeginMarkedContent] = &PageContentParseMarks.OpBeginMarkedContent;
        table[(int)ContentOperator.BeginMarkedContentProperties] = &PageContentParseMarks.OpBeginMarkedContentProperties;
        table[(int)ContentOperator.EndMarkedContent] = &PageContentParseMarks.OpEndMarkedContent;
    }

    /// <summary>Runs one operator, treating a damaged operand as a skipped operator.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader holding its operands.</param>
    internal static void Dispatch(PageContentParseState state, ContentOperator op, ref ContentReader reader)
    {
        var handler = PageContentParseDispatch.Handlers[(int)op];
        try
        {
            handler(state, ref reader);
        }
        catch (Exception ex) when (PageContentParse.IsRecoverable(ex))
        {
            // A damaged object affects only the operator that used it.
        }
    }
}
