// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Which way up a sheet of paper is used.</summary>
public enum PdfOrientation
{
    /// <summary>The sheet is taller than it is wide.</summary>
    Portrait = 0,

    /// <summary>The sheet is wider than it is tall.</summary>
    Landscape = 1,
}
