// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The working memory every slice of a stream uses, kept by the decoder so a slice does not allocate it again.</summary>
[DebuggerDisplay("H264SliceWork")]
internal sealed class H264SliceWork
{
    /// <summary>The samples along a side of a luma macroblock.</summary>
    private const int LumaSide = 16;

    /// <summary>The samples along a side of a chroma macroblock.</summary>
    private const int ChromaSide = 8;

    /// <summary>The number of chroma planes.</summary>
    private const int ChromaPlanes = 2;

    /// <summary>Gets the macroblock being decoded.</summary>
    internal H264CurrentMb Mb { get; } = new();

    /// <summary>Gets the macroblock the intra decoder inside a CAVLC inter slice decoder works on, which is not the inter decoder's.</summary>
    internal H264CurrentMb IntraMb { get; } = new();

    /// <summary>Gets the CABAC context states.</summary>
    internal byte[] Contexts { get; } = new byte[H264CabacContexts.Count];

    /// <summary>Gets the luma samples predicted from list 0 of a bi-predicted block.</summary>
    internal byte[] Luma0 { get; } = new byte[LumaSide * LumaSide];

    /// <summary>Gets the luma samples predicted from list 1 of a bi-predicted block.</summary>
    internal byte[] Luma1 { get; } = new byte[LumaSide * LumaSide];

    /// <summary>Gets the Cb then Cr samples predicted from list 0 of a bi-predicted block.</summary>
    internal byte[] Chroma0 { get; } = new byte[ChromaPlanes * ChromaSide * ChromaSide];

    /// <summary>Gets the Cb then Cr samples predicted from list 1 of a bi-predicted block.</summary>
    internal byte[] Chroma1 { get; } = new byte[ChromaPlanes * ChromaSide * ChromaSide];
}
