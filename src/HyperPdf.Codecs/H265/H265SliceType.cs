// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Names the values of slice_type (Table 7-7).</summary>
internal static class H265SliceType
{
    /// <summary>A slice that predicts from two reference lists.</summary>
    internal const int B = 0;

    /// <summary>A slice that predicts from one reference list.</summary>
    internal const int P = 1;

    /// <summary>A slice that uses only intra prediction.</summary>
    internal const int I = 2;
}
