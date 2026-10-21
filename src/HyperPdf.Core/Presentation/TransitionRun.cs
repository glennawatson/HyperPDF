// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Presentation;

/// <summary>A transition playing on the canvas.</summary>
/// <param name="IsRunning">Whether it is still playing.</param>
/// <param name="Step">What is played.</param>
/// <param name="Started">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
[DebuggerDisplay("TransitionRun: {Step}, running={IsRunning}")]
public readonly record struct TransitionRun(bool IsRunning, PresentationStep Step, long Started)
{
    /// <summary>Gets how far the transition has played, from 0 to 1, eased so it starts and ends gently.</summary>
    public double Progress
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TransitionTiming.GetProgress(Step.DurationSeconds, Started);
    }

    /// <summary>Starts the effect of a page now shown.</summary>
    /// <param name="step">The step for the page, or <see langword="null"/> when presenting ended.</param>
    /// <param name="now">The time now, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <returns>The run, or a run that is not playing when the page asks for no effect.</returns>
    public static TransitionRun Start(PresentationStep? step, long now) =>
        step is { Effect: not TransitionEffect.None } playing ? new(true, playing, now) : default;
}
