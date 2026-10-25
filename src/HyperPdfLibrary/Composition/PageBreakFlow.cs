// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A page break: it takes no room and makes the next item start a page.</summary>
[DebuggerDisplay("PageBreakFlow")]
internal sealed class PageBreakFlow : Flowable
{
    /// <inheritdoc/>
    internal override bool PageBreakBefore => true;

    /// <inheritdoc/>
    internal override float MeasureStart(FlowCursor cursor) => 0F;

    /// <inheritdoc/>
    internal override PlaceResult Place(FlowCursor cursor) => PlaceResult.Finished;
}
