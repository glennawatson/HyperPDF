// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Detection;

/// <summary>The kinds of place to write found on a printed or scanned form.</summary>
public enum FormRegionKind
{
    /// <summary>A ruled line to write on.</summary>
    Underline = 0,

    /// <summary>A box to write in.</summary>
    Box = 1,

    /// <summary>A row of equal boxes, or a line with evenly spaced ticks, for one character each.</summary>
    Comb = 2,
}
