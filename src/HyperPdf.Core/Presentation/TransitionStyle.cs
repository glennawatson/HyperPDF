// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Presentation;

/// <summary>The transition styles a page can ask for (ISO 32000-2 Table 161).</summary>
public enum TransitionStyle
{
    /// <summary>The new page replaces the old one at once.</summary>
    Replace = 0,

    /// <summary>Two lines sweep across the page, revealing the new page.</summary>
    Split = 1,

    /// <summary>Strips sweep across the page, revealing the new page.</summary>
    Blinds = 2,

    /// <summary>A box grows or shrinks to reveal the new page.</summary>
    Box = 3,

    /// <summary>A single line sweeps across the page, revealing the new page.</summary>
    Wipe = 4,

    /// <summary>The old page dissolves into the new one.</summary>
    Dissolve = 5,

    /// <summary>A glittering line sweeps across the page.</summary>
    Glitter = 6,

    /// <summary>The new page flies in.</summary>
    Fly = 7,

    /// <summary>The new page pushes the old one out.</summary>
    Push = 8,

    /// <summary>The new page slides over the old one.</summary>
    Cover = 9,

    /// <summary>The old page slides away, uncovering the new one.</summary>
    Uncover = 10,

    /// <summary>The old page fades into the new one.</summary>
    Fade = 11,
}
