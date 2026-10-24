// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>What the header of a slice says.</summary>
/// <param name="NalRefIdc">The reference indication of the NAL unit.</param>
/// <param name="IsIdr">Whether the slice is part of an IDR picture.</param>
/// <param name="FirstMb">The number of the first macroblock in the slice.</param>
/// <param name="SliceType">The slice type, from 0 to 4.</param>
/// <param name="PpsId">The number of the picture parameter set.</param>
/// <param name="Picture">The part that names the picture.</param>
/// <param name="Refs">The part that says which pictures the slice refers to.</param>
/// <param name="Marking">How the picture marks the reference pictures.</param>
/// <param name="Tail">The end of the header.</param>
[DebuggerDisplay("H264SliceHeader: first {FirstMb}, type {SliceType}, frame {Picture.FrameNum}")]
internal sealed record H264SliceHeader(
    int NalRefIdc,
    bool IsIdr,
    int FirstMb,
    int SliceType,
    int PpsId,
    H264PictureId Picture,
    H264RefSetup Refs,
    H264Marking Marking,
    H264SliceTail Tail)
{
    /// <summary>The slice type of a P slice.</summary>
    internal const int P = 0;

    /// <summary>The slice type of a B slice.</summary>
    internal const int B = 1;

    /// <summary>The slice type of an I slice.</summary>
    internal const int I = 2;

    /// <summary>The slice type of an SP slice.</summary>
    internal const int Sp = 3;

    /// <summary>The slice type of an SI slice.</summary>
    internal const int Si = 4;

    /// <summary>Gets a value indicating whether the slice is an intra slice.</summary>
    internal bool IsIntra => SliceType is I or Si;

    /// <summary>Gets a value indicating whether the slice is a P slice.</summary>
    internal bool IsP => SliceType is P or Sp;

    /// <summary>Gets a value indicating whether the slice is a B slice.</summary>
    internal bool IsB => SliceType == B;
}
