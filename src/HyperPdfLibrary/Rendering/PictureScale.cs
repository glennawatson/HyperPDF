// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>What a recorded picture depends on besides the page: the zoom band it serves and the layer zoom class.</summary>
/// <param name="UpperScale">The upper device scale of the zoom band, which picks image resolution.</param>
/// <param name="ZoomClass">The layer zoom class, which decides layers with zoom limits.</param>
[DebuggerDisplay("PictureScale: {UpperScale} class {ZoomClass}")]
internal readonly record struct PictureScale(float UpperScale, int ZoomClass);
