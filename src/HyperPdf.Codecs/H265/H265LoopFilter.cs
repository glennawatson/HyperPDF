// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Applies the in-loop filters to a decoded picture: deblocking, then sample adaptive offset (8.7).</summary>
internal static class H265LoopFilter
{
    /// <summary>Filters a picture.</summary>
    /// <param name="context">The slice context of the picture.</param>
    internal static void Apply(H265SliceContext context)
    {
        H265Deblocking.Apply(context);
        H265Sao.Apply(context);
    }
}
