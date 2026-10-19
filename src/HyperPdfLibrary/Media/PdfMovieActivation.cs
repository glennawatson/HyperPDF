// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>How a movie plays when activated (<c>/A</c> of a movie annotation).</summary>
/// <param name="Start">The start time as written (a number of seconds, a time string or a title), or null.</param>
/// <param name="Duration">The duration as written, or null.</param>
/// <param name="Rate">The play rate; 1 is normal speed.</param>
/// <param name="Volume">The volume from -1 to 1.</param>
/// <param name="ShowControls">Whether to show a controller bar.</param>
/// <param name="Mode">The play mode: Once, Open, Repeat or Palindrome.</param>
/// <param name="Synchronous">Whether the viewer waits for the movie to finish.</param>
/// <param name="FullWindowScale">The <c>/FWScale</c> numerator and denominator, or an empty array.</param>
/// <param name="FullWindowPosition">The <c>/FWPosition</c> horizontal and vertical fractions, or an empty array.</param>
[DebuggerDisplay("PdfMovieActivation: {Mode}")]
public sealed record PdfMovieActivation(
    string? Start,
    string? Duration,
    double Rate,
    double Volume,
    bool ShowControls,
    string Mode,
    bool Synchronous,
    int[] FullWindowScale,
    double[] FullWindowPosition);
