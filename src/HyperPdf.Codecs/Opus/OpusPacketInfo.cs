// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What the table of contents of an Opus packet says about its frames.</summary>
/// <param name="Mode">The coding the frames use.</param>
/// <param name="Bandwidth">The audio bandwidth.</param>
/// <param name="FrameSamples">The samples at 48 kHz in each frame.</param>
/// <param name="Stereo">Whether the frames are stereo.</param>
/// <param name="FrameCount">The frames in the packet.</param>
internal readonly record struct OpusPacketInfo(OpusMode Mode, OpusBandwidth Bandwidth, int FrameSamples, bool Stereo, int FrameCount);
