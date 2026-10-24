// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The picture order counts of the two fields of a frame.</summary>
/// <param name="Top">The top field's count.</param>
/// <param name="Bottom">The bottom field's count.</param>
internal readonly record struct H264FieldCounts(int Top, int Bottom);
