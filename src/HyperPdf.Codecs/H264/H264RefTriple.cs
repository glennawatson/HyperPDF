// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The reference indices of the three neighbours that motion prediction looks at.</summary>
/// <param name="A">The neighbour to the left.</param>
/// <param name="B">The neighbour above.</param>
/// <param name="C">The neighbour above and to the right, or above and to the left when that is missing.</param>
internal readonly record struct H264RefTriple(int A, int B, int C);
