// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>An image's decoded /SMask or stencil /Mask stream, ready to apply to the image's pixels.</summary>
/// <param name="Data">The decoded mask, which has pixels.</param>
/// <param name="Matte">The colour a soft mask's image was blended with, or <see langword="null"/>.</param>
internal sealed record DecodedImageMask(PdfImageData Data, MatteColor? Matte);
