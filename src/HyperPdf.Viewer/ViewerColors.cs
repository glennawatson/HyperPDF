// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Theming;

namespace HyperPdf.Viewer;

/// <summary>The colours the viewer draws with, as 0xAARRGGBB, taken from the user's theme.</summary>
/// <param name="Canvas">The background behind the pages.</param>
/// <param name="Shadow">The shadow under a page.</param>
/// <param name="Selection">The fill over selected text.</param>
/// <param name="Hit">The fill over a search hit.</param>
/// <param name="Accent">The outline of the current search hit, the focus ring and the caret.</param>
/// <param name="Ink">The ink of shapes and strokes being drawn.</param>
/// <param name="Spoken">The fill over the words being read aloud.</param>
/// <param name="Dim">The fill that dims the lines not being read.</param>
[DebuggerDisplay("ViewerColors canvas {Canvas}")]
public readonly record struct ViewerColors(uint Canvas, uint Shadow, uint Selection, uint Hit, uint Accent, uint Ink, uint Spoken, uint Dim)
{
    /// <summary>A fully opaque alpha.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The alpha of fills that lie over text, so the text stays readable.</summary>
    private const uint Wash = 0x55000000;

    /// <summary>The alpha of the page shadow and of dimmed lines.</summary>
    private const uint Faint = 0x40000000;

    /// <summary>The colour bits of a 0xRRGGBB value.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>The dark ink of shapes being drawn.</summary>
    private const uint DrawingInk = 0x202020;

    /// <summary>Takes the viewer colours from a colour scheme.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The colours.</returns>
    public static ViewerColors From(ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var accent = scheme.Accent & ColorMask;
        return new(
            Opaque | (scheme.Canvas & ColorMask),
            Faint,
            Wash | (scheme.Selection & ColorMask),
            Wash | accent,
            Opaque | accent,
            Opaque | DrawingInk,
            Wash | accent,
            Faint | (scheme.Canvas & ColorMask));
    }
}
