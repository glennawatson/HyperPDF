// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>How a track is coded and the bytes its decoder is set up with, as a container works them out from the file.</summary>
/// <param name="Codec">The coding.</param>
/// <param name="Configuration">The decoder's configuration bytes.</param>
internal readonly record struct CodecSetup(MediaCodec Codec, ReadOnlyMemory<byte> Configuration);
