// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>How long a reference picture list is and how its entries are reordered.</summary>
/// <param name="Count">The pictures in the list.</param>
/// <param name="Modification">The entries to pick from the initial list, or <see langword="null"/> to keep the initial order.</param>
internal readonly record struct H265ListShape(int Count, int[]? Modification);
