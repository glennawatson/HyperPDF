// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// A redundant 5 ms CELT frame at the end of a SILK or hybrid frame (RFC 6716 section 4.5.1). The encoder adds one when the stream changes
/// between modes, so the change can be cross-faded instead of cut.
/// </summary>
/// <param name="KeptLength">The bytes of the frame that are not redundancy.</param>
/// <param name="Bytes">The bytes of the redundant frame; zero when the frame has none.</param>
/// <param name="CeltToSilk">Whether the stream is changing from CELT to SILK, so the redundant frame covers the start of this one rather than the end.</param>
internal readonly record struct OpusRedundancy(int KeptLength, int Bytes, bool CeltToSilk)
{
    /// <summary>Gets a value indicating whether the frame has a redundant frame.</summary>
    internal bool Present => Bytes > 0;
}
