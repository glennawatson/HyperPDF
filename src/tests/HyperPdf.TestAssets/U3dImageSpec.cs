// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>A continuation image of a test texture declaration.</summary>
/// <param name="Compression">The picture format: 1 JPEG colour, 2 PNG, 3 JPEG greyscale, 4 TIFF.</param>
/// <param name="Channels">The channels the image supplies: 1 alpha, 2 blue, 4 green, 8 red, 16 luminance.</param>
/// <param name="External"><see langword="true"/> when the image is in another file.</param>
public readonly record struct U3dImageSpec(byte Compression, byte Channels, bool External);
