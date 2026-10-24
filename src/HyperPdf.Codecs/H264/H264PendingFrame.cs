// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>A decoded frame waiting to be output.</summary>
/// <param name="Poc">The frame's picture order count.</param>
/// <param name="Frame">The frame.</param>
internal readonly record struct H264PendingFrame(int Poc, VideoFrame Frame);
