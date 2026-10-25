// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>Where a barcode's modules sit on the page.</summary>
/// <param name="X">The left edge of the first column.</param>
/// <param name="Y">The bottom edge of the last row.</param>
/// <param name="Width">The width of a module.</param>
/// <param name="Height">The height of a module.</param>
internal readonly record struct BarcodeLayout(float X, float Y, float Width, float Height);
