// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The image channels in output order, and how many of them the file declares as colour.</summary>
/// <param name="Channels">The channels: colour first, then opacity, then others.</param>
/// <param name="ColourCount">The colour channels a channel definition box declares, or zero when the file does not say.</param>
internal readonly record struct JpxChannelSet(JpxChannel[] Channels, int ColourCount);
