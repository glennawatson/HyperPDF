// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How a block of text sits between the top and bottom edges of the space it is drawn in.</summary>
public enum PdfVerticalAlignment
{
    /// <summary>The block starts at the top edge.</summary>
    Top = 0,

    /// <summary>The block is centred.</summary>
    Middle = 1,

    /// <summary>The block ends at the bottom edge.</summary>
    Bottom = 2,
}
