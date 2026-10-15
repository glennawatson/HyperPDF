// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How the text of a table cell lines up inside the cell.</summary>
public enum PdfTableAlignment
{
    /// <summary>Against the left padding.</summary>
    Left = 0,

    /// <summary>Centred between the paddings.</summary>
    Center = 1,

    /// <summary>Against the right padding, as numbers are usually set.</summary>
    Right = 2,
}
