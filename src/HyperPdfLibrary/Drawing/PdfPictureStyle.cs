// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Drawing;

/// <summary>What a recording device needs to know about how its picture will be shown.</summary>
/// <param name="Quality">How edges, thin lines and scaled images are drawn.</param>
/// <param name="Device">
/// The matrix from the picture's space to device pixels, without the tile offset, when the picture serves only that
/// matrix; null when the picture is replayed at other scales.
/// </param>
[DebuggerDisplay("PdfPictureStyle: device {Device}")]
public readonly record struct PdfPictureStyle(PdfRenderQuality Quality, Matrix3x2? Device);
