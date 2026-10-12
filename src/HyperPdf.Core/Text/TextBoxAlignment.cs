// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text;

/// <summary>How the lines of a text box sit across its width.</summary>
public enum TextBoxAlignment
{
    /// <summary>Lines start at the left edge.</summary>
    Left = 0,

    /// <summary>Lines are centred.</summary>
    Center = 1,

    /// <summary>Lines end at the right edge.</summary>
    Right = 2,
}
