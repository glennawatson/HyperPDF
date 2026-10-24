// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The part of a coefficient block that can hold non-zero values.</summary>
/// <param name="LastColumn">The index of the last column with a non-zero coefficient.</param>
/// <param name="LastRow">The index of the last row with a non-zero coefficient.</param>
internal readonly record struct H265BlockExtent(int LastColumn, int LastRow);
