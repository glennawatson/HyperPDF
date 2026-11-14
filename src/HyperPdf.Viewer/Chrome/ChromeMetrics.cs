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
    private const double SidebarUnits = 280;

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

    /// <summary>Makes the sizes for a display scale.</summary>
    /// <param name="scale">The device pixels of one unit.</param>
    /// <returns>The sizes.</returns>
    public static ChromeMetrics For(double scale) => new(
        Scaled(TabUnits, scale),
        Scaled(ToolbarUnits, scale),
        Scaled(StatusUnits, scale),
        Scaled(TextUnits, scale),
        Scaled(PaddingUnits, scale),
        Scaled(GapUnits, scale),
        Scaled(RowUnits, scale),
        Scaled(TabWidthUnits, scale),
        Scaled(PopupUnits, scale),
        Scaled(DialogUnits, scale),
        Scaled(SidebarUnits, scale),
        Scaled(1, scale));

    /// <summary>Scales a size, never to less than one pixel.</summary>
    /// <param name="units">The size at a scale of one.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The size in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Scaled(double units, double scale) => Math.Max(1, (int)Math.Round(units * scale));
}
