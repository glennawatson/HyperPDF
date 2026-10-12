// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Platform;

/// <summary>Colours and fonts taken from the desktop environment, as 0xAARRGGBB values.</summary>
[DebuggerDisplay("DesktopPalette: {SchemeName} (dark: {IsDark})")]
public sealed record DesktopPalette
{
    /// <summary>Gets the colour scheme name.</summary>
    public string? SchemeName { get; init; }

    /// <summary>Gets the window background.</summary>
    public uint WindowBackground { get; init; }

    /// <summary>Gets the window text colour.</summary>
    public uint WindowForeground { get; init; }

    /// <summary>Gets the background of content views such as lists.</summary>
    public uint ViewBackground { get; init; }

    /// <summary>Gets the text colour of content views.</summary>
    public uint ViewForeground { get; init; }

    /// <summary>Gets the button background.</summary>
    public uint ButtonBackground { get; init; }

    /// <summary>Gets the button text colour.</summary>
    public uint ButtonForeground { get; init; }

    /// <summary>Gets the header (title and tool bar) background.</summary>
    public uint HeaderBackground { get; init; }

    /// <summary>Gets the accent (selection) colour.</summary>
    public uint Accent { get; init; }

    /// <summary>Gets the text colour drawn on the accent colour.</summary>
    public uint AccentForeground { get; init; }

    /// <summary>Gets the inactive text colour.</summary>
    public uint InactiveForeground { get; init; }

    /// <summary>Gets the user interface font family.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the user interface font size in points.</summary>
    public double? FontSizePoints { get; init; }

    /// <summary>Gets the desktop animation speed factor (0 = instant, 1 = normal), when set.</summary>
    public double? AnimationDurationFactor { get; init; }

    /// <summary>Gets the text cursor blink interval in milliseconds (0 = no blinking), when set.</summary>
    public int? CursorBlinkRateMilliseconds { get; init; }

    /// <summary>Gets a value indicating whether the palette is dark.</summary>
    public bool IsDark { get; init; }
}
