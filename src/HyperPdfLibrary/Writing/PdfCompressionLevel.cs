// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>How hard the writers work to make Flate data small.</summary>
public enum PdfCompressionLevel
{
    /// <summary>Uses zlib's default balance of size and speed (level 6).</summary>
    Balanced = 0,

    /// <summary>The quickest level (level 1); the data is larger.</summary>
    Fastest = 1,

    /// <summary>The smallest data zlib makes (level 9); slowest.</summary>
    Smallest = 2,
}
