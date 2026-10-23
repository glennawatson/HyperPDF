// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>What happened when a flowable item was placed on a page.</summary>
internal enum PlaceResult
{
    /// <summary>All of the item is placed.</summary>
    Finished = 0,

    /// <summary>Part of the item, or none of it, is placed; the rest needs a new page.</summary>
    NeedsPage = 1,
}
