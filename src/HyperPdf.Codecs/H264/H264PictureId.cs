// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The part of a slice header that names the picture the slice belongs to.</summary>
/// <param name="FrameNum">The frame number.</param>
/// <param name="FieldPic">Whether the slice is part of a field.</param>
/// <param name="BottomField">Whether the field is the bottom field.</param>
/// <param name="IdrPicId">The number that tells IDR pictures in a row apart.</param>
/// <param name="PicOrderCntLsb">The coded low bits of the picture order count.</param>
/// <param name="DeltaPicOrderCntBottom">The offset of the bottom field's picture order count in a frame.</param>
/// <param name="DeltaPicOrderCnt0">The first offset of the picture order count, for type 1.</param>
/// <param name="DeltaPicOrderCnt1">The second offset of the picture order count, for type 1.</param>
internal sealed record H264PictureId(int FrameNum, bool FieldPic, bool BottomField, int IdrPicId, int PicOrderCntLsb, int DeltaPicOrderCntBottom, int DeltaPicOrderCnt0, int DeltaPicOrderCnt1);
