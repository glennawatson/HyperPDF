// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>One image to encode with <see cref="Jbig2Encoder.EncodeShared(IReadOnlyList{Jbig2PageInput}, Jbig2EncodeOptions, CancellationToken)"/>.</summary>
/// <param name="Rows">The packed rows, 0 for black, in the layout <see cref="Jbig2Encoder"/> takes.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("Jbig2PageInput: {Width}x{Height}")]
public readonly record struct Jbig2PageInput(ReadOnlyMemory<byte> Rows, int Width, int Height);
