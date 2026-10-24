// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A frame placed in the flow, at a position on the page, or floating to an edge with text wrapping round it.</summary>
[DebuggerDisplay("FrameFlow: {_frame.Position}, {_frame.Wrap}")]
internal sealed class FrameFlow : Flowable
{
    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>The frame.</summary>
    private readonly PdfFrame _frame;

    /// <summary>Where the frame is: its indent and structure parent.</summary>
    private readonly FlattenScope _scope;

    /// <summary>The frame's content, once its width is known.</summary>
    private StackResult? _content;

    /// <summary>The frame's box, with its size, once the width is known; the position is worked out when the frame is placed.</summary>
    private PdfCanvasBox _size;

    /// <summary>Initializes a new instance of the <see cref="FrameFlow"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="scope">Where the frame is: its indent and structure parent.</param>
    internal FrameFlow(LayoutEnv env, PdfFrame frame, FlattenScope scope)
    {
        _env = env;
        _frame = frame;
        _scope = scope;
    }

    /// <inheritdoc/>
    internal override float SpaceBefore => TakesRoomInFlow ? _frame.SpaceBefore : 0F;

    /// <inheritdoc/>
    internal override float SpaceAfter => TakesRoomInFlow ? _frame.SpaceAfter : 0F;

    /// <summary>Gets a value indicating whether the frame is in the flow and pushes the text below it down.</summary>
    private bool TakesRoomInFlow => _frame.Position == PdfFramePosition.Flow && _frame.Wrap == PdfFrameWrap.TopAndBottom;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor)
    {
        EnsureLaidOut(cursor);
        return TakesRoomInFlow ? _size.Height : 0F;
    }

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor)
    {
        EnsureLaidOut(cursor);
        var inFlow = _frame.Position == PdfFramePosition.Flow;
        if (inFlow && _frame.Wrap != PdfFrameWrap.None && _size.Height > cursor.Remaining && !cursor.AtTop)
        {
            return PlaceResult.NeedsPage;
        }

        var box = GetBox(cursor);
        Draw(cursor, box);
        if (inFlow && _frame.Wrap == PdfFrameWrap.TopAndBottom)
        {
            cursor.Y -= _size.Height;
        }
        else if (inFlow && _frame.Wrap is PdfFrameWrap.Left or PdfFrameWrap.Right)
        {
            AddExclusion(cursor, box);
        }

        return PlaceResult.Finished;
    }

    /// <summary>Lays the frame's content out the first time it is needed.</summary>
    /// <param name="cursor">The cursor.</param>
    private void EnsureLaidOut(FlowCursor cursor)
    {
        if (_content is not null)
        {
            return;
        }

        var insets = GetInsets();
        var available = cursor.Width - _scope.Indent;
        var width = MathF.Min(_frame.Width ?? available, available);
        var inner = MathF.Max(1F, width - insets.Horizontal);
        var node = _env.CreateNode("Div", _scope.Parent);
        _content = StackLayout.Build(_env, _frame.Blocks, inner, cursor.Artifact, new(0F, node, null), CancellationToken.None);
        _size = new(0F, 0F, width, _frame.Height ?? (_content.Height + insets.Vertical));
    }

    /// <summary>Works out the room the frame's borders and padding take on each side.</summary>
    /// <returns>The room on each side.</returns>
    private PdfInsets GetInsets()
    {
        var borders = _frame.Borders;
        var padding = _frame.Padding;
        return new(
            (borders.Left?.Width ?? 0F) + padding.Left,
            (borders.Top?.Width ?? 0F) + padding.Top,
            (borders.Right?.Width ?? 0F) + padding.Right,
            (borders.Bottom?.Width ?? 0F) + padding.Bottom);
    }

    /// <summary>Works out where the frame goes on the page.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>The frame's box.</returns>
    private PdfCanvasBox GetBox(FlowCursor cursor)
    {
        switch (_frame.Position)
        {
            case PdfFramePosition.Page:
            {
                return new(_frame.Left, cursor.Page.Section.Setup.PageHeight - _frame.Top - _size.Height, _size.Width, _size.Height);
            }

            case PdfFramePosition.Margin:
            {
                return new(cursor.Left + _frame.Left, cursor.Top - _frame.Top - _size.Height, _size.Width, _size.Height);
            }

            default:
            {
                return new(GetFlowLeft(cursor), cursor.Y - _size.Height, _size.Width, _size.Height);
            }
        }
    }

    /// <summary>Works out the left edge of a frame in the flow.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>The x.</returns>
    private float GetFlowLeft(FlowCursor cursor)
    {
        var spare = cursor.Width - _scope.Indent - _size.Width;
        var side = _frame.Wrap switch
        {
            PdfFrameWrap.Right => PdfTextAlignment.Right,
            PdfFrameWrap.Left => PdfTextAlignment.Left,
            _ => _frame.Alignment,
        };
        var shift = side switch
        {
            PdfTextAlignment.Center => spare * Half,
            PdfTextAlignment.Right => spare,
            _ => 0F,
        };
        return cursor.Left + _scope.Indent + MathF.Max(0F, shift);
    }

    /// <summary>Draws the frame's shading, content and borders.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="box">The frame's box.</param>
    private void Draw(FlowCursor cursor, PdfCanvasBox box)
    {
        var sink = cursor.Page.Commands;
        if (_frame.Shading is { } shading)
        {
            sink.Add(new FillCommand(null, PdfArtifactKind.Background, box, shading));
        }

        var insets = GetInsets();
        sink.AddRange(_content!.PlaceAt(box.X + insets.Left, box.Top - insets.Top));
        BorderDrawer.Draw(sink, _frame.Borders, box, cursor.Artifact);
    }

    /// <summary>Makes the text that follows leave room beside a floating frame.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <param name="box">The frame's box.</param>
    private void AddExclusion(FlowCursor cursor, PdfCanvasBox box)
    {
        var gap = _frame.SpaceAfter;
        var left = _frame.Wrap == PdfFrameWrap.Left ? box.Right - cursor.Left + gap : 0F;
        var right = _frame.Wrap == PdfFrameWrap.Right ? cursor.Left + cursor.Width - box.X + gap : 0F;
        cursor.Page.Exclusions.Add(new(box.Top, box.Y - gap, left, right));
    }
}
