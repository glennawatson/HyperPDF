// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Where one frame lies in an Opus packet.</summary>
/// <param name="Offset">The offset of the frame's first byte in the packet.</param>
/// <param name="Length">The bytes in the frame; zero for a frame that was lost and is concealed.</param>
internal readonly record struct OpusFrame(int Offset, int Length);
