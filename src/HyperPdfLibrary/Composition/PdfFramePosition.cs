// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>What a <see cref="PdfFrame"/>'s position is measured from.</summary>
public enum PdfFramePosition
{
    /// <summary>The frame sits in the flow of the text, where it comes in the content.</summary>
    Flow = 0,

    /// <summary>The frame is placed from the top left corner of the text area of the page it comes on.</summary>
    Margin = 1,

    /// <summary>The frame is placed from the top left corner of the paper of the page it comes on.</summary>
    Page = 2,
}
