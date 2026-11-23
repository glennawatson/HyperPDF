// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The sizes of the window's controls in device pixels, for one display scale.</summary>
/// <param name="TabHeight">The height of the tab strip.</param>
/// <param name="ToolbarHeight">The height of the toolbar.</param>
/// <param name="StatusHeight">The height of the status bar.</param>
/// <param name="TextSize">The text size.</param>
/// <param name="Padding">The space between a control's edge and its text.</param>
/// <param name="Gap">The space between controls.</param>
/// <param name="RowHeight">The height of a menu or list row.</param>
/// <param name="TabWidth">The widest a tab gets.</param>
/// <param name="PopupWidth">The narrowest a menu gets.</param>
/// <param name="DialogWidth">The width of a dialog.</param>
/// <param name="SidebarWidth">The width of the side panel.</param>
/// <param name="Line">The thickness of a border.</param>
[DebuggerDisplay("ChromeMetrics: text {TextSize} px")]
public readonly record struct ChromeMetrics(
    int TabHeight,
    int ToolbarHeight,
    int StatusHeight,
    int TextSize,
    int Padding,
    int Gap,
    int RowHeight,
    int TabWidth,
    int PopupWidth,
    int DialogWidth,
    int SidebarWidth,
    int Line)
{
    /// <summary>The side panel width at a scale of one.</summary>
    private const double SidebarUnits = 340;

    /// <summary>The height of the tab strip at a scale of one.</summary>
    private const double TabUnits = 36;

    /// <summary>The height of the toolbar at a scale of one. Controls are tall enough to press with ease.</summary>
    private const double ToolbarUnits = 40;

    /// <summary>The height of the status bar at a scale of one.</summary>
    private const double StatusUnits = 28;

    /// <summary>The text size at a scale of one.</summary>
    private const double TextUnits = 14;

    /// <summary>The padding at a scale of one.</summary>
    private const double PaddingUnits = 12;

    /// <summary>The gap at a scale of one.</summary>
    private const double GapUnits = 4;

    /// <summary>The row height at a scale of one.</summary>
    private const double RowUnits = 30;

    /// <summary>The widest tab at a scale of one.</summary>
    private const double TabWidthUnits = 220;

    /// <summary>The narrowest menu at a scale of one.</summary>
    private const double PopupUnits = 220;

    /// <summary>The dialog width at a scale of one.</summary>
    private const double DialogUnits = 560;

    /// <summary>The parts of the text size an icon is as tall as: ten sevenths, so text of 14 has icons of 20.</summary>
    private const int IconParts = 10;

    /// <summary>The parts the text size is divided into for the size of an icon.</summary>
    private const int TextParts = 7;

    /// <summary>The text size in points at the standard interface size (14 pixels at 96 DPI).</summary>
    private const double DefaultPoints = 10.5;

    /// <summary>The smallest interface text size in points.</summary>
    private const double MinPoints = 8.0;

    /// <summary>The largest interface text size in points.</summary>
    private const double MaxPoints = 24.0;

    /// <summary>Gets the side of an icon beside text, in device pixels.</summary>
    public int IconSize => TextSize * IconParts / TextParts;

    /// <summary>Makes the sizes for a display scale.</summary>
    /// <param name="scale">The device pixels of one unit.</param>
    /// <returns>The sizes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChromeMetrics For(double scale) => For(scale, null);

    /// <summary>Makes the sizes for a display scale and an optional interface text size in points.</summary>
    /// <param name="scale">The device pixels of one unit.</param>
    /// <param name="fontSizePoints">The chosen text size in points, or <see langword="null"/> for the default.</param>
    /// <returns>The sizes.</returns>
    public static ChromeMetrics For(double scale, double? fontSizePoints)
    {
        var fontFactor = fontSizePoints is > 0 ? Math.Clamp(fontSizePoints.Value, MinPoints, MaxPoints) / DefaultPoints : 1.0;
        var textScale = scale * fontFactor;
        var widthScale = scale * Math.Max(1.0, fontFactor);
        return new(
            Scaled(TabUnits, textScale),
            Scaled(ToolbarUnits, textScale),
            Scaled(StatusUnits, textScale),
            Scaled(TextUnits, textScale),
            Scaled(PaddingUnits, textScale),
            Scaled(GapUnits, textScale),
            Scaled(RowUnits, textScale),
            Scaled(TabWidthUnits, widthScale),
            Scaled(PopupUnits, widthScale),
            Scaled(DialogUnits, widthScale),
            Scaled(SidebarUnits, widthScale),
            Scaled(1, scale));
    }

    /// <summary>Scales a size, never to less than one pixel.</summary>
    /// <param name="units">The size at a scale of one.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The size in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Scaled(double units, double scale) => Math.Max(1, (int)Math.Round(units * scale));
}
