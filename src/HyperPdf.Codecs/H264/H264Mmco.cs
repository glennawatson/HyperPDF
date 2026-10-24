// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>One memory management control operation of the reference picture marking.</summary>
/// <param name="Operation">The operation, from 1 to 6.</param>
/// <param name="Difference">The difference of picture numbers minus 1, for operations 1 and 3.</param>
/// <param name="LongTermPicNum">The long-term picture number, for operation 2.</param>
/// <param name="LongTermFrameIdx">The long-term frame index, for operations 3 and 6, or the maximum plus 1 for operation 4.</param>
internal readonly record struct H264Mmco(int Operation, int Difference, int LongTermPicNum, int LongTermFrameIdx);
