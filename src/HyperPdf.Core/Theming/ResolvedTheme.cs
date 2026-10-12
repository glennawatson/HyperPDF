// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Theming;

/// <summary>Everything the interface needs to draw itself, resolved from settings and the desktop.</summary>
/// <param name="Scheme">The colour scheme.</param>
/// <param name="PageTone">The page tone to render with.</param>
/// <param name="FontFamily">The interface font family, or <see langword="null"/> for the default.</param>
/// <param name="FontSizePoints">The interface font size in points, or <see langword="null"/> for the default.</param>
/// <param name="ReduceMotion">Whether transitions are turned off.</param>
/// <param name="SteadyCaret">Whether the text cursor stays steady.</param>
/// <param name="ShowLabels">Whether tool bar buttons show text labels.</param>
[DebuggerDisplay("ResolvedTheme: {Scheme.Name}, motion reduced: {ReduceMotion}")]
public sealed record ResolvedTheme(ColorScheme Scheme, PageTone PageTone, string? FontFamily, double? FontSizePoints, bool ReduceMotion, bool SteadyCaret, bool ShowLabels);
