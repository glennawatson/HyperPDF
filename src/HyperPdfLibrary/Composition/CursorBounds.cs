// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Line bounds that leave room for the floating frames on a page.</summary>
[DebuggerDisplay("CursorBounds")]
internal sealed class CursorBounds : ILineBounds
{
    /// <summary>The page whose frames the lines make room for.</summary>
    private readonly PagePlan _page;

    /// <summary>Initializes a new instance of the <see cref="CursorBounds"/> class.</summary>
    /// <param name="page">The page whose frames the lines make room for.</param>
    internal CursorBounds(PagePlan page) => _page = page;

    /// <inheritdoc/>
    public LineSpan GetSpan(float top, float height)
    {
        var left = 0F;
        var right = 0F;
        foreach (var exclusion in _page.Exclusions)
        {
            var overlaps = top > exclusion.Bottom && top - height < exclusion.Top;
            left = overlaps ? MathF.Max(left, exclusion.Left) : left;
            right = overlaps ? MathF.Max(right, exclusion.Right) : right;
        }

        return new(left, right);
    }
}
