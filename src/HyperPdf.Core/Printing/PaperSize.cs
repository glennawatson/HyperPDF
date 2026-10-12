// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Printing;

/// <summary>The paper several pages are printed onto.</summary>
public enum PaperSize
{
    /// <summary>ISO A4, 210 × 297 mm.</summary>
    A4 = 0,

    /// <summary>US Letter, 8.5 × 11 in.</summary>
    Letter = 1,

    /// <summary>ISO A3, 297 × 420 mm.</summary>
    A3 = 2,

    /// <summary>ISO A5, 148 × 210 mm.</summary>
    A5 = 3,

    /// <summary>US Legal, 8.5 × 14 in.</summary>
    Legal = 4,

    /// <summary>US Tabloid, 11 × 17 in.</summary>
    Tabloid = 5,
}
