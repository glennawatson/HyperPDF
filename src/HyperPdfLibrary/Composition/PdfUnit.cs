// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>A unit of length that <see cref="PdfUnits"/> converts to points.</summary>
public enum PdfUnit
{
    /// <summary>A point, 1/72 of an inch: the unit the library works in.</summary>
    Point = 0,

    /// <summary>An inch, 72 points.</summary>
    Inch = 1,

    /// <summary>A millimetre.</summary>
    Millimeter = 2,

    /// <summary>A centimetre.</summary>
    Centimeter = 3,

    /// <summary>A pica, 12 points.</summary>
    Pica = 4,
}
