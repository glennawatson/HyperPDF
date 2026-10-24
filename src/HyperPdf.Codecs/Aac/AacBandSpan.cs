// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Aac;

/// <summary>Where one band of one group of windows lies in a spectrum.</summary>
/// <param name="FirstWindow">The first window of the group.</param>
/// <param name="Length">The windows in the group.</param>
/// <param name="WindowSize">The coefficients in a window.</param>
/// <param name="Start">The first coefficient of the band within a window.</param>
/// <param name="End">The coefficient after the band.</param>
internal readonly record struct AacBandSpan(int FirstWindow, int Length, int WindowSize, int Start, int End)
{
    /// <summary>Gets the coefficients in the band of one window.</summary>
    internal int Width => End - Start;

    /// <summary>Finds where the band starts in the spectrum for a window of the group.</summary>
    /// <param name="window">The window, counted from the first of the group.</param>
    /// <returns>The index of the band's first coefficient.</returns>
    internal int SpectrumStart(int window) => ((FirstWindow + window) * WindowSize) + Start;
}
