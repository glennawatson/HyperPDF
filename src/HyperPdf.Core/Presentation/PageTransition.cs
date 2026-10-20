// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Presentation;

/// <summary>What a page asks for when it appears in a presentation: an effect, and how long it stays before the next page.</summary>
/// <param name="Style">The effect.</param>
/// <param name="DurationSeconds">How long the effect lasts, in seconds.</param>
/// <param name="Vertical">Split and Blinds: whether the lines run vertically.</param>
/// <param name="Inward">Split, Box and Fly: whether the effect moves inwards rather than outwards.</param>
/// <param name="Direction">The direction in degrees counter-clockwise from left to right, or -1 for none.</param>
/// <param name="AdvanceAfterSeconds">The seconds before the page advances by itself, or <see langword="null"/>.</param>
[DebuggerDisplay("PageTransition: {Style} {DurationSeconds}s, advance {AdvanceAfterSeconds}")]
public sealed record PageTransition(TransitionStyle Style, double DurationSeconds, bool Vertical, bool Inward, int Direction, double? AdvanceAfterSeconds);
