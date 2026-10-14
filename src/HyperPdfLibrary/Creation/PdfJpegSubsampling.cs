// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How much of the colour detail of a three- or four-component JPEG is kept relative to the brightness detail.</summary>
public enum PdfJpegSubsampling
{
    /// <summary>Full colour detail when the quality is 90 or more, otherwise a quarter of it (4:2:0).</summary>
    Automatic = 0,

    /// <summary>Full colour detail (4:4:4).</summary>
    Chroma444 = 1,

    /// <summary>Half the colour detail across (4:2:2).</summary>
    Chroma422 = 2,

    /// <summary>A quarter of the colour detail: half across and half down (4:2:0).</summary>
    Chroma420 = 3,

    /// <summary>Half the colour detail down (4:4:0).</summary>
    Chroma440 = 4,

    /// <summary>A quarter of the colour detail across (4:1:1).</summary>
    Chroma411 = 5,
}
