// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What the Opus layer tells the SILK layer about the packet it is decoding.</summary>
/// <param name="Channels">The channels of the output.</param>
/// <param name="InternalChannels">The channels the stream codes.</param>
/// <param name="InternalRateHz">The sampling rate of the SILK layer: 8000, 12000 or 16000.</param>
/// <param name="PayloadMs">The milliseconds of sound in the packet's frame: 10, 20, 40 or 60.</param>
internal readonly record struct SilkControl(int Channels, int InternalChannels, int InternalRateHz, int PayloadMs);
