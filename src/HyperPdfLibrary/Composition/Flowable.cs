// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Something laid out from top to bottom and across pages: a paragraph, a rule, a figure, a table or a page break.</summary>
[DebuggerDisplay("Flowable")]
internal abstract class Flowable
{
    /// <summary>Gets the space in points the item wants above it.</summary>
    internal virtual float SpaceBefore => 0F;

    /// <summary>Gets the space in points the item leaves below it.</summary>
    internal virtual float SpaceAfter => 0F;

    /// <summary>Gets a value indicating whether the item must stay on the same page as the start of the next one.</summary>
    internal virtual bool KeepWithNext => false;

    /// <summary>Gets a value indicating whether the item starts a new page.</summary>
    internal virtual bool PageBreakBefore => false;

    /// <summary>Measures the part of the item that must fit on a page with what it is kept with: its first lines, or all of it when it cannot split.</summary>
    /// <param name="cursor">The cursor the item will be placed at.</param>
    /// <returns>The height in points, without spacing.</returns>
    internal abstract float MeasureStart(FlowCursor cursor);

    /// <summary>Places as much of the item as fits at the cursor and moves the cursor down.</summary>
    /// <param name="cursor">The cursor.</param>
    /// <returns>
    /// <see cref="PlaceResult.Finished"/> when all the item is placed, or <see cref="PlaceResult.NeedsPage"/> when the rest
    /// needs a new page. At the top of a page the item always places something.
    /// </returns>
    internal abstract PlaceResult Place(FlowCursor cursor);
}
