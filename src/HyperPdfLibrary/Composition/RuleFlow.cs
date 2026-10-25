// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A horizontal rule across the text area.</summary>
[DebuggerDisplay("RuleFlow: {_rule.Thickness} pt")]
internal sealed class RuleFlow : Flowable
{
    /// <summary>Half, for centring the line on its space.</summary>
    private const float Half = 0.5F;

    /// <summary>The rule.</summary>
    private readonly PdfRule _rule;

    /// <summary>The indent from the left edge of the text area.</summary>
    private readonly float _indent;

    /// <summary>Initializes a new instance of the <see cref="RuleFlow"/> class.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="indent">The indent from the left edge of the text area.</param>
    internal RuleFlow(PdfRule rule, float indent)
    {
        _rule = rule;
        _indent = indent;
    }

    /// <inheritdoc/>
    internal override float SpaceBefore => _rule.SpaceBefore;

    /// <inheritdoc/>
    internal override float SpaceAfter => _rule.SpaceAfter;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor) => _rule.Thickness;

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor)
    {
        if (cursor.Remaining < _rule.Thickness && !cursor.AtTop)
        {
            return PlaceResult.NeedsPage;
        }

        var y = cursor.Y - (_rule.Thickness * Half);
        var pen = new PdfPen(_rule.Color, _rule.Thickness);
        cursor.Page.Commands.Add(new StrokeCommand(null, cursor.Artifact, pen, new(cursor.Left + _indent, y), new(cursor.Left + cursor.Width, y)));
        cursor.Y -= _rule.Thickness;
        return PlaceResult.Finished;
    }
}
