// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A coding unit as the transform tree sees it.</summary>
/// <param name="X">The column of its top-left luma sample.</param>
/// <param name="Y">The row of its top-left luma sample.</param>
/// <param name="Log2Size">The base 2 logarithm of its side.</param>
/// <param name="Intra">Whether it is intra predicted.</param>
/// <param name="PartMode">How it is divided into prediction blocks; one of the <see cref="H265PartMode"/> values.</param>
/// <param name="Bypass">Whether transform and quantisation are bypassed.</param>
/// <param name="MaxTrafoDepth">The deepest level its transform tree may reach.</param>
internal readonly record struct H265CodingUnit(int X, int Y, int Log2Size, bool Intra, int PartMode, bool Bypass, int MaxTrafoDepth)
{
    /// <summary>Gets a value indicating whether an intra coding unit is divided into four prediction blocks.</summary>
    internal bool IntraSplit => Intra && PartMode == H265PartMode.NxN;
}
