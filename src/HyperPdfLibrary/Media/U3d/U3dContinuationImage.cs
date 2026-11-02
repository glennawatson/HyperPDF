// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>One continuation image of a texture declaration (ECMA-363 9.8.5.1.4).</summary>
/// <param name="Compression">The picture format: 1 JPEG colour, 2 PNG, 3 JPEG greyscale, 4 TIFF.</param>
/// <param name="Channels">The channels the image supplies: 1 alpha, 2 blue, 4 green, 8 red, 16 luminance.</param>
/// <param name="IsExternal"><see langword="true"/> when the image is in another file, which is never loaded.</param>
[DebuggerDisplay("U3dContinuationImage: type {Compression}")]
internal readonly record struct U3dContinuationImage(byte Compression, byte Channels, bool IsExternal);
