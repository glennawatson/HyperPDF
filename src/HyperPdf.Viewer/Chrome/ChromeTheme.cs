// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Theming;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The colours of the window's controls, as ARGB.</summary>
/// <param name="Bar">The background of the tab strip, toolbar and status bar.</param>
/// <param name="Surface">The background of the tab shown, of menus, dialogs and text boxes.</param>
/// <param name="Text">The text colour.</param>
/// <param name="Faded">The text colour of a control that cannot be pressed.</param>
/// <param name="Border">The colour of borders and separator lines.</param>
/// <param name="Accent">The colour that marks the chosen control.</param>
/// <param name="Hover">The wash drawn over a control under the pointer.</param>
/// <param name="Scrim">The wash drawn over the window behind a dialog.</param>
[DebuggerDisplay("ChromeTheme: bar {Bar}")]
public readonly record struct ChromeTheme(uint Bar, uint Surface, uint Text, uint Faded, uint Border, uint Accent, uint Hover, uint Scrim)
{
    /// <summary>The alpha of an opaque colour.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The colour channels of a colour.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>The alpha of the hover wash, light enough to keep the text contrast.</summary>
    private const uint HoverAlpha = 0x30000000;

    /// <summary>The scrim: a see-through black that dims the window without hiding it.</summary>
    private const uint ScrimColor = 0x66000000;

    /// <summary>Makes the colours of a colour scheme.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The colours.</returns>
    public static ChromeTheme From(ColorScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        return new(
            Opaque | (scheme.Header & ColorMask),
            Opaque | (scheme.View & ColorMask),
            Opaque | (scheme.Text & ColorMask),
            Opaque | (scheme.InactiveText & ColorMask),
            Opaque | (scheme.Border & ColorMask),
            Opaque | (scheme.Accent & ColorMask),
            HoverAlpha | (scheme.Accent & ColorMask),
            ScrimColor);
    }
}
