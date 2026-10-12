// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Theming;

/// <summary>A complete set of interface colours, as 0xRRGGBB values.</summary>
/// <param name="Name">The display name.</param>
/// <param name="IsDark">Whether the scheme is dark.</param>
/// <param name="Window">The window and tab bar background.</param>
/// <param name="Header">The tool bar background.</param>
/// <param name="View">The sidebar and list background.</param>
/// <param name="Canvas">The background behind pages.</param>
/// <param name="Text">Normal text.</param>
/// <param name="InactiveText">Secondary and disabled text.</param>
/// <param name="Border">Edges between regions and controls.</param>
/// <param name="Accent">The one strong colour: the selected tab marker and focus.</param>
/// <param name="Selection">The background of selected list items.</param>
/// <param name="Tints">Icon colours by action.</param>
/// <param name="PageTone">The page tone that suits the scheme.</param>
[DebuggerDisplay("ColorScheme: {Name}")]
public sealed record ColorScheme(
    string Name,
    bool IsDark,
    uint Window,
    uint Header,
    uint View,
    uint Canvas,
    uint Text,
    uint InactiveText,
    uint Border,
    uint Accent,
    uint Selection,
    IconTints Tints,
    PageTone PageTone);
