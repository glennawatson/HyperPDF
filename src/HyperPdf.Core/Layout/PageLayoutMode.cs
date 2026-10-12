// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Layout;

/// <summary>How pages are arranged.</summary>
public enum PageLayoutMode
{
    /// <summary>One page per row.</summary>
    Single = 0,

    /// <summary>Two pages per row, odd pages on the left.</summary>
    Dual = 1,

    /// <summary>Two pages per row, with the first page alone on the right, as in a printed book.</summary>
    DualCover = 2,
}
