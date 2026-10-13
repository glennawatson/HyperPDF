// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The marks a text markup annotation draws.</summary>
internal enum MarkupStyle
{
    /// <summary>Not a text markup subtype.</summary>
    None = 0,

    /// <summary>A filled quadrilateral (<c>/Highlight</c>).</summary>
    Highlight = 1,

    /// <summary>A line along the bottom (<c>/Underline</c>).</summary>
    Underline = 2,

    /// <summary>A line through the middle (<c>/StrikeOut</c>).</summary>
    StrikeOut = 3,

    /// <summary>A zigzag along the bottom (<c>/Squiggly</c>).</summary>
    Squiggly = 4,
}
