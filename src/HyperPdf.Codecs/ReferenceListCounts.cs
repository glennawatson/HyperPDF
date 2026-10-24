// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>The numbers of pictures in the two reference lists of a slice.</summary>
/// <param name="L0">The pictures in list 0.</param>
/// <param name="L1">The pictures in list 1.</param>
internal readonly record struct ReferenceListCounts(int L0, int L1);
