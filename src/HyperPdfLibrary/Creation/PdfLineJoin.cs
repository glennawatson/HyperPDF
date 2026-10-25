// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How the corners of a stroked path are finished (ISO 32000-2 8.4.3.4).</summary>
public enum PdfLineJoin
{
    /// <summary>The outer edges meet in a point, unless it is longer than the miter limit.</summary>
    Miter = 0,

    /// <summary>The corner is rounded.</summary>
    Round = 1,

    /// <summary>The corner is cut off flat.</summary>
    Bevel = 2,
}
