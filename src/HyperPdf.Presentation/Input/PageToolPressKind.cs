// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Input;

/// <summary>What a pointer press does when a page tool or measuring is active.</summary>
public enum PageToolPressKind
{
    /// <summary>The press is not for a page tool.</summary>
    None = 0,

    /// <summary>The press stops auto-scrolling.</summary>
    StopAutoScroll = 1,

    /// <summary>The press starts panning the pages.</summary>
    Pan = 2,

    /// <summary>The press starts choosing an area to zoom to or copy.</summary>
    Area = 3,

    /// <summary>The press adds a point to a measurement.</summary>
    Measure = 4,

    /// <summary>The press only gives the canvas the keyboard focus.</summary>
    Focus = 5,
}
