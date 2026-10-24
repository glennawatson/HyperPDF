// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A position in a square block.</summary>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
internal readonly record struct H265ScanPosition(byte X, byte Y);
