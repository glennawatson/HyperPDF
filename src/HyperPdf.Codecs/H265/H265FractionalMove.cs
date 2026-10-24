// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A motion vector split into whole samples and a fraction of a sample.</summary>
/// <param name="WholeX">The whole samples to move across.</param>
/// <param name="WholeY">The whole samples to move down.</param>
/// <param name="FractionX">The fraction across, in quarter samples for luma and eighth samples for chroma.</param>
/// <param name="FractionY">The fraction down.</param>
internal readonly record struct H265FractionalMove(int WholeX, int WholeY, int FractionX, int FractionY);
