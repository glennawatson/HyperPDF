// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The widths of fixed size fields in a slice header.</summary>
internal static class H265SliceHeaderBits
{
    /// <summary>The bits of colour_plane_id.</summary>
    internal const int ColourPlane = 2;

    /// <summary>The bits in a byte.</summary>
    internal const int Byte = 8;
}
