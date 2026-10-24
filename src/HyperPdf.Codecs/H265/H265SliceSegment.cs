// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Where a slice segment sits in its picture and in its payload.</summary>
/// <param name="First">Whether it is the first segment of the picture.</param>
/// <param name="NoOutputOfPriorPics">Whether pictures waiting for output are dropped when this IRAP picture starts.</param>
/// <param name="PpsId">The number of the picture parameter set in use.</param>
/// <param name="Dependent">Whether it continues the slice before it and takes the settings of that slice.</param>
/// <param name="Address">The raster scan address of its first coding tree block.</param>
/// <param name="DataOffset">The index in the payload of the first byte of slice data.</param>
/// <param name="EntryPoints">The size in bytes of each subset of slice data but the last, counting emulation prevention bytes.</param>
internal sealed record H265SliceSegment(bool First, bool NoOutputOfPriorPics, int PpsId, bool Dependent, int Address, int DataOffset, int[] EntryPoints);
