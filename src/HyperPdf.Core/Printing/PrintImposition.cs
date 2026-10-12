// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Printing;

/// <summary>How pages are arranged on the printed sheets.</summary>
public enum PrintImposition
{
    /// <summary>Pages in order, one or several to a sheet.</summary>
    Pages = 0,

    /// <summary>Two pages side by side on each side of a sheet, ordered so the folded stack reads as a booklet.</summary>
    Booklet = 1,

    /// <summary>Each page enlarged across several sheets, with a small overlap for joining them.</summary>
    Poster = 2,
}
