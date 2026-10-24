// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The rows an interpolation filter pass reads and writes.</summary>
/// <param name="Stride">The samples in a row of the work area.</param>
/// <param name="Width">The samples to filter in each row.</param>
/// <param name="Height">The rows to filter.</param>
internal readonly record struct H265FilterArea(int Stride, int Width, int Height);
