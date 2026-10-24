// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>Where one NAL unit lies in a packet.</summary>
/// <param name="Offset">The index of the NAL unit's first byte, which is its header.</param>
/// <param name="Length">The length of the NAL unit in bytes, including its header.</param>
internal readonly record struct NalRange(int Offset, int Length);
