// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The PDF/X level a file claims, from the ISO 15930 series.</summary>
public enum PdfXLevel
{
    /// <summary>The file makes no PDF/X claim.</summary>
    None = 0,

    /// <summary>The X-1a level, from ISO 15930 parts 1 and 4.</summary>
    X1a = 1,

    /// <summary>The X-3 level, from ISO 15930 parts 3 and 6.</summary>
    X3 = 2,

    /// <summary>The X-4 level, from ISO 15930 part 7.</summary>
    X4 = 3,

    /// <summary>The X-4p level, from ISO 15930 part 7, which may reference an external output profile.</summary>
    X4p = 4,

    /// <summary>The X-5g, X-5pg and X-5n levels, from ISO 15930 part 8, which may reference external content.</summary>
    X5 = 5,

    /// <summary>The file names a PDF/X version this library does not know.</summary>
    Unknown = 6,
}
