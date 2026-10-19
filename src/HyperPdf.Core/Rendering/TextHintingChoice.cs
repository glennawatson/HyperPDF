// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>How small text is fitted to the screen's pixel grid.</summary>
public enum TextHintingChoice
{
    /// <summary>Text sits where the page puts it, to a quarter of a pixel, which keeps spacing even.</summary>
    None = 0,

    /// <summary>Text sits on whole rows of pixels, so each line has an even, crisp bottom edge.</summary>
    Slight = 1,

    /// <summary>Text sits on whole pixels both ways, which gives the crispest stems and uneven spacing.</summary>
    Full = 2,
}
