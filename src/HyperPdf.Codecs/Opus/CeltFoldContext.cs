// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What is known about a band when its folding is chosen.</summary>
/// <param name="Band">The band.</param>
/// <param name="Scale">The short blocks in the frame, as a power of two.</param>
/// <param name="Blocks">The short blocks a band is made of.</param>
/// <param name="Channels">The channels.</param>
/// <param name="Width">The entries in the band.</param>
/// <param name="TimeFrequency">The time-frequency change of the band.</param>
/// <param name="LowbandOffset">The band to fold from, or zero for none.</param>
/// <param name="Spread">The spreading decision.</param>
internal readonly record struct CeltFoldContext(int Band, int Scale, int Blocks, int Channels, int Width, int TimeFrequency, int LowbandOffset, int Spread);
