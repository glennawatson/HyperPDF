// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Content;
namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Operators operations over its owned state.</summary>
internal static unsafe class ContentOperators
{
    /// <summary>The handlers, indexed by <see cref = "ContentOperator"/>; operators the interpreter ignores point at a no-op.</summary>
    private static readonly delegate*<ContentInterpreter, ref ContentReader, void>[] Handlers = ContentOperators.CreateHandlers();

    /// <summary>Builds the dispatch table.</summary>
    /// <returns>The table.</returns>
    internal static delegate*<ContentInterpreter, ref ContentReader, void>[] CreateHandlers()
    {
        var table = new delegate*<ContentInterpreter, ref ContentReader, void>[Enum.GetValues<ContentOperator>().Length];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = &ContentOperators.OpIgnore;
        }

        ContentOperators.RegisterPaths(table);
        ContentOperators.RegisterState(table);
        ContentOperators.RegisterColors(table);
        ContentOperators.RegisterText(table);
        ContentOperators.RegisterOther(table);
        return table;
    }

    /// <summary>Registers path construction, painting and clipping.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterPaths(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.MoveTo] = &ContentPaths.OpMoveTo;
        table[(int)ContentOperator.LineTo] = &ContentPaths.OpLineTo;
        table[(int)ContentOperator.CurveTo] = &ContentPaths.OpCurveTo;
        table[(int)ContentOperator.CurveToV] = &ContentPaths.OpCurveToV;
        table[(int)ContentOperator.CurveToY] = &ContentPaths.OpCurveToY;
        table[(int)ContentOperator.ClosePath] = &ContentPaths.OpClosePath;
        table[(int)ContentOperator.Rectangle] = &ContentPaths.OpRectangle;
        table[(int)ContentOperator.Fill] = &ContentPaths.OpFill;
        table[(int)ContentOperator.FillEvenOdd] = &ContentPaths.OpFillEvenOdd;
        table[(int)ContentOperator.Stroke] = &ContentPaths.OpStroke;
        table[(int)ContentOperator.CloseStroke] = &ContentPaths.OpCloseStroke;
        table[(int)ContentOperator.FillStroke] = &ContentPaths.OpFillStroke;
        table[(int)ContentOperator.FillStrokeEvenOdd] = &ContentPaths.OpFillStrokeEvenOdd;
        table[(int)ContentOperator.CloseFillStroke] = &ContentPaths.OpCloseFillStroke;
        table[(int)ContentOperator.CloseFillStrokeEvenOdd] = &ContentPaths.OpCloseFillStrokeEvenOdd;
        table[(int)ContentOperator.EndPath] = &ContentPaths.OpEndPath;
        table[(int)ContentOperator.Clip] = &ContentPaths.OpClip;
        table[(int)ContentOperator.ClipEvenOdd] = &ContentPaths.OpClipEvenOdd;
    }

    /// <summary>Registers the graphics state operators.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterState(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.Save] = &ContentGraphicsState.OpSave;
        table[(int)ContentOperator.Restore] = &ContentGraphicsState.OpRestore;
        table[(int)ContentOperator.ConcatMatrix] = &ContentGraphicsState.OpConcatMatrix;
        table[(int)ContentOperator.SetLineWidth] = &ContentGraphicsState.OpSetLineWidth;
        table[(int)ContentOperator.SetLineCap] = &ContentGraphicsState.OpSetLineCap;
        table[(int)ContentOperator.SetLineJoin] = &ContentGraphicsState.OpSetLineJoin;
        table[(int)ContentOperator.SetMiterLimit] = &ContentGraphicsState.OpSetMiterLimit;
        table[(int)ContentOperator.SetDash] = &ContentGraphicsState.OpSetDash;
        table[(int)ContentOperator.SetGraphicsState] = &ContentGraphicsState.OpSetGraphicsState;
        table[(int)ContentOperator.SetRenderingIntent] = &ContentColorRendering.OpSetRenderingIntent;
    }

    /// <summary>Registers the colour operators.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterColors(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.SetFillGray] = &ContentColors.OpSetFillGray;
        table[(int)ContentOperator.SetStrokeGray] = &ContentColors.OpSetStrokeGray;
        table[(int)ContentOperator.SetFillRgb] = &ContentColors.OpSetFillRgb;
        table[(int)ContentOperator.SetStrokeRgb] = &ContentColors.OpSetStrokeRgb;
        table[(int)ContentOperator.SetFillCmyk] = &ContentColors.OpSetFillCmyk;
        table[(int)ContentOperator.SetStrokeCmyk] = &ContentColors.OpSetStrokeCmyk;
        table[(int)ContentOperator.SetFillColorSpace] = &ContentColors.OpSetFillColorSpace;
        table[(int)ContentOperator.SetStrokeColorSpace] = &ContentColors.OpSetStrokeColorSpace;
        table[(int)ContentOperator.SetFillColor] = &ContentColors.OpSetFillColor;
        table[(int)ContentOperator.SetFillColorN] = &ContentColors.OpSetFillColor;
        table[(int)ContentOperator.SetStrokeColor] = &ContentColors.OpSetStrokeColor;
        table[(int)ContentOperator.SetStrokeColorN] = &ContentColors.OpSetStrokeColor;
    }

    /// <summary>Registers the text operators.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterText(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.BeginText] = &ContentText.OpBeginText;
        table[(int)ContentOperator.EndText] = &ContentText.OpEndText;
        table[(int)ContentOperator.SetCharacterSpacing] = &ContentText.OpSetCharacterSpacing;
        table[(int)ContentOperator.SetWordSpacing] = &ContentText.OpSetWordSpacing;
        table[(int)ContentOperator.SetHorizontalScaling] = &ContentText.OpSetHorizontalScaling;
        table[(int)ContentOperator.SetLeading] = &ContentText.OpSetLeading;
        table[(int)ContentOperator.SetFont] = &ContentText.OpSetFont;
        table[(int)ContentOperator.SetRenderMode] = &ContentText.OpSetRenderMode;
        table[(int)ContentOperator.SetRise] = &ContentText.OpSetRise;
        table[(int)ContentOperator.MoveText] = &ContentText.OpMoveText;
        table[(int)ContentOperator.MoveTextSetLeading] = &ContentText.OpMoveTextSetLeading;
        table[(int)ContentOperator.SetTextMatrix] = &ContentText.OpSetTextMatrix;
        table[(int)ContentOperator.NextLine] = &ContentText.OpNextLine;
        table[(int)ContentOperator.ShowText] = &ContentText.OpShowText;
        table[(int)ContentOperator.ShowTextArray] = &ContentText.OpShowTextArray;
        table[(int)ContentOperator.NextLineShowText] = &ContentText.OpNextLineShowText;
        table[(int)ContentOperator.SetSpacingNextLineShowText] = &ContentText.OpSpacingNextLineShowText;
        table[(int)ContentOperator.SetGlyphWidthAndBounds] = &ContentText.OpSetGlyphWidthAndBounds;
    }

    /// <summary>Registers XObjects, inline images, shadings and marked content.</summary>
    /// <param name = "table">The table.</param>
    internal static void RegisterOther(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.PaintXObject] = &ContentXObjects.OpPaintXObject;
        table[(int)ContentOperator.BeginInlineImage] = &ContentXObjects.OpInlineImage;
        table[(int)ContentOperator.PaintShading] = &ContentXObjects.OpPaintShading;
        table[(int)ContentOperator.BeginMarkedContent] = &ContentMarked.OpBeginMarkedContent;
        table[(int)ContentOperator.BeginMarkedContentProperties] = &ContentMarked.OpBeginMarkedContentProperties;
        table[(int)ContentOperator.EndMarkedContent] = &ContentMarked.OpEndMarkedContent;
    }

    /// <summary>Does nothing; the handler for operators that do not affect rendering.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpIgnore(ContentInterpreter self, ref ContentReader reader)
    {
        // Compatibility sections, flatness, marked points and stray inline image keywords change nothing drawn.
    }

    /// <summary>Runs one operator, treating a damaged operand as a skipped operator.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader holding its operands.</param>
    internal static void Dispatch(ContentInterpreter self, ContentOperator op, ref ContentReader reader)
    {
        var handler = ContentOperators.Handlers[(int)op];
        try
        {
            handler(self, ref reader);
        }
        catch (Exception ex) when (ContentExecution.IsRecoverable(ex))
        {
            // A damaged object affects only the operator that used it.
        }
    }
}
