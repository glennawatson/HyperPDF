// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The scratch arrays the inverse transform of a CELT frame works in.</summary>
internal sealed class CeltSynthesisWorkspace
{
    /// <summary>Gets the block being built: a frame of samples and the overlap after it.</summary>
    internal float[] Block { get; } = new float[CeltDecoder.MaxFrameSize + CeltDecoder.Overlap];

    /// <summary>Gets the scratch space of the transform.</summary>
    internal float[] Transform { get; } = new float[CeltMdct.ScratchSize];
}
