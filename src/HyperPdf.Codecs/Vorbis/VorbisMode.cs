// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Vorbis;

/// <summary>A mode: the block size an audio packet uses, and the mapping that says how its channels are coded.</summary>
/// <param name="IsLongBlock">Whether the packet uses the long block size.</param>
/// <param name="Mapping">The index of the mapping.</param>
internal readonly record struct VorbisMode(bool IsLongBlock, int Mapping);
