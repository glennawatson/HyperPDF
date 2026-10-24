// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>A decoded picture: its samples, and what the decoder keeps about every 4x4 block for the neighbours and later pictures that need it.</summary>
[DebuggerDisplay("H265Picture: POC {Poc} {Width}x{Height}")]
internal sealed class H265Picture
{
    /// <summary>The side of the grid the block information is kept on.</summary>
    internal const int GridLog2 = 2;

    /// <summary>The block kind of a block that was not decoded.</summary>
    internal const byte NotDecoded = 0;

    /// <summary>The block kind of an intra predicted block.</summary>
    internal const byte Intra = 1;

    /// <summary>The block kind of an inter predicted block.</summary>
    internal const byte Inter = 2;

    /// <summary>The bit of a block's flags that says the block was coded with transform and quantisation bypass.</summary>
    internal const byte BypassFlag = 1;

    /// <summary>The bit of a block's flags that says the block holds pulse code modulation samples.</summary>
    internal const byte PcmFlag = 2;

    /// <summary>The bit of a block's flags that says the block was skipped.</summary>
    internal const byte SkipFlag = 4;

    /// <summary>The bit of a block's flags that says the transform block holding it has luma coefficients.</summary>
    internal const byte CodedFlag = 128;

    /// <summary>The bit of a block's flags that says a transform block edge lies on its left side.</summary>
    internal const byte TransformEdgeLeft = 8;

    /// <summary>The bit of a block's flags that says a transform block edge lies on its top side.</summary>
    internal const byte TransformEdgeTop = 16;

    /// <summary>The bit of a block's flags that says a prediction block edge lies on its left side.</summary>
    internal const byte PredictionEdgeLeft = 32;

    /// <summary>The bit of a block's flags that says a prediction block edge lies on its top side.</summary>
    internal const byte PredictionEdgeTop = 64;

    /// <summary>The reference state of a picture that is not used for reference.</summary>
    internal const int Unused = 0;

    /// <summary>The reference state of a picture used for short-term reference.</summary>
    internal const int ShortTerm = 1;

    /// <summary>The reference state of a picture used for long-term reference.</summary>
    internal const int LongTerm = 2;

    /// <summary>Initializes a new instance of the <see cref="H265Picture"/> class.</summary>
    /// <param name="sps">The sequence parameter set the picture uses.</param>
    /// <param name="layout">The layout of the picture.</param>
    internal H265Picture(H265Sps sps, H265PictureLayout layout)
    {
        Width = sps.Width;
        Height = sps.Height;
        ChromaWidth = sps.ChromaFormat == ChromaSampling.Monochrome ? 0 : Width / sps.SubWidthC;
        ChromaHeight = sps.ChromaFormat == ChromaSampling.Monochrome ? 0 : Height / sps.SubHeightC;
        Luma = new ushort[Width * Height];
        Cb = new ushort[ChromaWidth * ChromaHeight];
        Cr = new ushort[ChromaWidth * ChromaHeight];
        GridWidth = (Width + (1 << GridLog2) - 1) >> GridLog2;
        GridHeight = (Height + (1 << GridLog2) - 1) >> GridLog2;
        var grid = GridWidth * GridHeight;
        Kind = new byte[grid];
        Flags = new byte[grid];
        IntraMode = new byte[grid];
        CtDepth = new byte[grid];
        QpY = new sbyte[grid];
        Motion = new H265PuMotion[grid];
        SliceOfCtb = new int[layout.CtbCount];
        Sao = new int[layout.CtbCount * H265SaoParams.IntsPerCtb];
        HeaderOfCtb = new H265SliceHeader?[layout.CtbCount];
        RefsIndexOfCtb = new int[layout.CtbCount];
        Array.Fill(SliceOfCtb, -1);
        Array.Fill(Motion, H265PuMotion.None);
        SliceRefs = [];
    }

    /// <summary>Gets the luma width in samples.</summary>
    internal int Width { get; }

    /// <summary>Gets the luma height in samples.</summary>
    internal int Height { get; }

    /// <summary>Gets the chroma width in samples, or 0 for monochrome video.</summary>
    internal int ChromaWidth { get; }

    /// <summary>Gets the chroma height in samples, or 0 for monochrome video.</summary>
    internal int ChromaHeight { get; }

    /// <summary>Gets the 4x4 blocks in a row.</summary>
    internal int GridWidth { get; }

    /// <summary>Gets the rows of 4x4 blocks.</summary>
    internal int GridHeight { get; }

    /// <summary>Gets the luma samples, row after row.</summary>
    internal ushort[] Luma { get; }

    /// <summary>Gets the Cb samples, row after row.</summary>
    internal ushort[] Cb { get; }

    /// <summary>Gets the Cr samples, row after row.</summary>
    internal ushort[] Cr { get; }

    /// <summary>Gets how each 4x4 block was predicted: not decoded, intra or inter.</summary>
    internal byte[] Kind { get; }

    /// <summary>Gets the flags of each 4x4 block.</summary>
    internal byte[] Flags { get; }

    /// <summary>Gets the luma intra prediction mode of each 4x4 block.</summary>
    internal byte[] IntraMode { get; }

    /// <summary>Gets the coding quadtree depth of each 4x4 block.</summary>
    internal byte[] CtDepth { get; }

    /// <summary>Gets the luma quantiser of each 4x4 block.</summary>
    internal sbyte[] QpY { get; }

    /// <summary>Gets the motion of each 4x4 block.</summary>
    internal H265PuMotion[] Motion { get; }

    /// <summary>Gets the slice address of each coding tree block, or -1 for a block not decoded.</summary>
    internal int[] SliceOfCtb { get; }

    /// <summary>Gets the header of the slice segment each coding tree block belongs to, or <see langword="null"/> for a block not decoded.</summary>
    internal H265SliceHeader?[] HeaderOfCtb { get; }

    /// <summary>Gets the index in <see cref="SliceRefs"/> of the slice each coding tree block belongs to.</summary>
    internal int[] RefsIndexOfCtb { get; }

    /// <summary>Gets the sample adaptive offset parameters of each coding tree block, in the layout of <see cref="H265SaoParams"/>.</summary>
    internal int[] Sao { get; }

    /// <summary>Gets the reference picture lists of each slice of the picture, which a later picture needs to follow the motion.</summary>
    internal List<H265SliceRefs> SliceRefs { get; }

    /// <summary>Gets or sets when the picture is shown, in seconds.</summary>
    internal double Time { get; set; }

    /// <summary>Gets or sets the picture order count.</summary>
    internal int Poc { get; set; }

    /// <summary>Gets or sets how the picture is kept for reference: 0 not at all, 1 as a short-term picture, 2 as a long-term picture.</summary>
    internal int ReferenceState { get; set; }

    /// <summary>Gets a value indicating whether the picture is used for long-term reference.</summary>
    internal bool IsLongTerm => ReferenceState == LongTerm;

    /// <summary>Gets or sets a value indicating whether the picture still has to be output.</summary>
    internal bool NeededForOutput { get; set; }

    /// <summary>Gets or sets how many pictures have been decoded since this one waited for output.</summary>
    internal int LatencyCount { get; set; }

    /// <summary>Gets or sets a value indicating whether a picture is only a stand-in made for a missing reference.</summary>
    internal bool IsGenerated { get; set; }

    /// <summary>Gets or sets the number of the sequence parameter set the picture was decoded with.</summary>
    internal H265Sps? Sps { get; set; }

    /// <summary>Finds whether the picture has the sizes a picture of a sequence and layout needs.</summary>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="layout">The layout of the picture.</param>
    /// <returns><see langword="true"/> when the picture can be recycled for it.</returns>
    internal bool Fits(H265Sps sps, H265PictureLayout layout) =>
        Width == sps.Width
        && Height == sps.Height
        && ChromaWidth == (sps.ChromaFormat == ChromaSampling.Monochrome ? 0 : sps.Width / sps.SubWidthC)
        && ChromaHeight == (sps.ChromaFormat == ChromaSampling.Monochrome ? 0 : sps.Height / sps.SubHeightC)
        && SliceOfCtb.Length == layout.CtbCount;

    /// <summary>Returns the picture to the state a new one has, so a picture nothing refers to any more can be decoded into again.</summary>
    internal void Recycle()
    {
        Array.Clear(Luma);
        Array.Clear(Cb);
        Array.Clear(Cr);
        Array.Clear(Kind);
        Array.Clear(Flags);
        Array.Clear(IntraMode);
        Array.Clear(CtDepth);
        Array.Clear(QpY);
        Array.Clear(Sao);
        Array.Clear(HeaderOfCtb);
        Array.Clear(RefsIndexOfCtb);
        Array.Fill(SliceOfCtb, -1);
        Array.Fill(Motion, H265PuMotion.None);
        SliceRefs.Clear();
        Time = 0;
        Poc = 0;
        ReferenceState = 0;
        NeededForOutput = false;
        LatencyCount = 0;
        IsGenerated = false;
        Sps = null;
    }

    /// <summary>Gets the offset in the 4x4 grid of a luma position.</summary>
    /// <param name="x">The column in samples.</param>
    /// <param name="y">The row in samples.</param>
    /// <returns>The index.</returns>
    internal int GridIndex(int x, int y) => ((y >> GridLog2) * GridWidth) + (x >> GridLog2);

    /// <summary>Gets the samples of a colour component.</summary>
    /// <param name="component">0 for luma, 1 for Cb, 2 for Cr.</param>
    /// <returns>The plane.</returns>
    internal ushort[] PlaneOf(int component)
    {
        if (component == 0)
        {
            return Luma;
        }

        return component == 1 ? Cb : Cr;
    }

    /// <summary>Gets the samples in a row of a colour component.</summary>
    /// <param name="component">0 for luma, otherwise a chroma component.</param>
    /// <returns>The stride.</returns>
    internal int StrideOf(int component) => component == 0 ? Width : ChromaWidth;
}
