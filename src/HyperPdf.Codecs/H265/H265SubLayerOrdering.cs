// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The decoded picture buffer limits that decide when a decoded picture may be output.</summary>
/// <param name="MaxDecPicBuffering">The most pictures held in the buffer.</param>
/// <param name="MaxNumReorder">The most pictures that may come before another in decoding order and after it in output order.</param>
/// <param name="MaxLatencyIncreasePlus1">Zero for no limit, otherwise one more than the extra pictures that may wait for output.</param>
internal readonly record struct H265SubLayerOrdering(int MaxDecPicBuffering, int MaxNumReorder, int MaxLatencyIncreasePlus1);
