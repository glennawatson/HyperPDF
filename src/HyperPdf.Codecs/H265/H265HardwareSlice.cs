// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H265;

/// <summary>One slice segment of a picture for a hardware decoder: its header, its reference lists, and where its NAL unit is in the picture's bytes.</summary>
/// <param name="Header">The slice segment header.</param>
/// <param name="List0">Reference picture list 0, in order.</param>
/// <param name="List1">Reference picture list 1, in order.</param>
/// <param name="Offset">Where the NAL unit starts in the picture's bytes.</param>
/// <param name="Length">The bytes of the NAL unit.</param>
[DebuggerDisplay("H265HardwareSlice: address {Header.Segment.Address}, {Length} bytes")]
internal sealed record H265HardwareSlice(H265SliceHeader Header, H265Picture[] List0, H265Picture[] List1, int Offset, int Length);
