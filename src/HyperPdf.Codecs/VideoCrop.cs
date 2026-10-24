// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>The frame cropping of a sequence: the units of pixels to drop from each edge.</summary>
/// <param name="Left">The units to drop from the left.</param>
/// <param name="Right">The units to drop from the right.</param>
/// <param name="Top">The units to drop from the top.</param>
/// <param name="Bottom">The units to drop from the bottom.</param>
internal readonly record struct VideoCrop(int Left, int Right, int Top, int Bottom);
