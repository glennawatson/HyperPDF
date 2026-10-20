// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Presentation;

/// <summary>What happens when a page appears in a presentation, once the reader's settings are taken into account.</summary>
/// <param name="Sequence">A number that changes with every page shown, so the same step played twice is still two plays.</param>
/// <param name="Style">The style the page asked for.</param>
/// <param name="Effect">How the viewer draws it; <see cref="TransitionEffect.None"/> shows the page at once.</param>
/// <param name="DurationSeconds">How long the effect lasts, in seconds.</param>
/// <param name="Vertical">Whether a split runs vertically.</param>
/// <param name="Inward">Whether the effect moves inwards rather than outwards.</param>
/// <param name="Direction">The direction in degrees counter-clockwise from left to right.</param>
/// <param name="IsApproximate">Whether the viewer draws the style as something close to it rather than as asked.</param>
/// <param name="AdvanceAfterSeconds">The seconds before the page advances by itself, or <see langword="null"/> to stay until the reader moves on.</param>
[DebuggerDisplay("PresentationStep: {Style} as {Effect}, advance {AdvanceAfterSeconds}")]
public readonly record struct PresentationStep(
    int Sequence,
    TransitionStyle Style,
    TransitionEffect Effect,
    double DurationSeconds,
    bool Vertical,
    bool Inward,
    int Direction,
    bool IsApproximate,
    double? AdvanceAfterSeconds);
