// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>One operation that changes the order of a reference picture list.</summary>
/// <param name="Idc">What the operation does: 0 and 1 move a short-term picture by a difference of picture numbers, 2 moves a long-term picture.</param>
/// <param name="Value">The difference of picture numbers minus 1, or the long-term picture number.</param>
internal readonly record struct H264RefModification(int Idc, int Value);
