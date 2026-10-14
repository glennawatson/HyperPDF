// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>One CCITT code word: its bits and how many there are.</summary>
/// <param name="Value">The code's bits, right-aligned.</param>
/// <param name="Length">The number of bits.</param>
internal readonly record struct CcittCode(int Value, int Length);
