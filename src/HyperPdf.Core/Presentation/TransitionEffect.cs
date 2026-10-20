// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Presentation;

/// <summary>How the viewer draws a page appearing.</summary>
public enum TransitionEffect
{
    /// <summary>The page appears at once.</summary>
    None = 0,

    /// <summary>The page fades in.</summary>
    Fade = 1,

    /// <summary>The page is revealed by a line sweeping across it.</summary>
    Wipe = 2,

    /// <summary>The page is revealed from the middle outwards, or from the edges inwards, along one axis.</summary>
    Split = 3,

    /// <summary>The page is revealed by a box growing from the middle, or shrinking in from the edges.</summary>
    Box = 4,

    /// <summary>The page slides in from one side.</summary>
    Slide = 5,
}
