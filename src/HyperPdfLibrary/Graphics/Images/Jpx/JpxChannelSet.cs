// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The image channels in output order, and how many of them the file declares as colour.</summary>
/// <param name="Channels">The channels: colour first, then opacity, then others.</param>
/// <param name="ColourCount">The colour channels a channel definition box declares, or zero when the file does not say.</param>
internal readonly record struct JpxChannelSet(JpxChannel[] Channels, int ColourCount)
{
    /// <summary>The <see cref="Opacity"/> of a file whose valid channel definition box declares no opacity channel.</summary>
    internal const int NoOpacity = -2;

    /// <summary>The <see cref="Opacity"/> of a file with no usable channel definition box.</summary>
    internal const int Undeclared = -1;

    /// <summary>
    /// Gets the index in <see cref="Channels"/> of the opacity channel a channel definition box declares,
    /// <see cref="NoOpacity"/> when the box declares none, or <see cref="Undeclared"/> when there is no usable box; the
    /// channel after the colour channels is then taken as opacity.
    /// </summary>
    internal int Opacity { get; init; } = Undeclared;

    /// <summary>Gets a value indicating whether the declared opacity channel is premultiplied into the colour channels.</summary>
    internal bool Premultiplied { get; init; }
}
