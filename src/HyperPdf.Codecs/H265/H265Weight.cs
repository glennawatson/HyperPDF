// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A prediction weight and offset.</summary>
/// <param name="Weight">The weight.</param>
/// <param name="Offset">The offset, already shifted to the bit depth.</param>
internal readonly record struct H265Weight(int Weight, int Offset);
