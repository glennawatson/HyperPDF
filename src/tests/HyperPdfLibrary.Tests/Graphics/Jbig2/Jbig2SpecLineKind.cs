// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>The kind of line in a standard Huffman table of the JBIG2 standard, annex B.</summary>
internal enum Jbig2SpecLineKind
{
    /// <summary>A line for a range of values that counts up from its low value.</summary>
    Normal = 0,

    /// <summary>The lower range line, which counts down from its low value.</summary>
    Lower = 1,

    /// <summary>The upper range line.</summary>
    Upper = 2,

    /// <summary>The out-of-band line.</summary>
    OutOfBand = 3,
}
