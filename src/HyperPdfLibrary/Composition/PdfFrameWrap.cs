// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>How the text round a <see cref="PdfFrame"/> behaves.</summary>
public enum PdfFrameWrap
{
    /// <summary>Text ignores the frame and runs under it, which suits a stamp or a watermark.</summary>
    None = 0,

    /// <summary>Text stops above the frame and carries on below it.</summary>
    TopAndBottom = 1,

    /// <summary>The frame sits at the left edge and the text that follows wraps down its right side.</summary>
    Left = 2,

    /// <summary>The frame sits at the right edge and the text that follows wraps down its left side.</summary>
    Right = 3,
}
