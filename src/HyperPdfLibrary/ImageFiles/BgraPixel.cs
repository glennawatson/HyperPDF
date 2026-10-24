// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>One pixel as blue, green, red and alpha bytes.</summary>
/// <param name="Blue">The blue channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Red">The red channel.</param>
/// <param name="Alpha">The alpha channel.</param>
[DebuggerDisplay("BgraPixel: {Red}, {Green}, {Blue}, {Alpha}")]
internal readonly record struct BgraPixel(byte Blue, byte Green, byte Red, byte Alpha);
