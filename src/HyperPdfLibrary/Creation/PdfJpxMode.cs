// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>Whether a JPEG 2000 image keeps every sample exactly.</summary>
public enum PdfJpxMode
{
    /// <summary>The reversible 5/3 wavelet and reversible component transform: decoding gives back every sample.</summary>
    Lossless = 0,

    /// <summary>The irreversible 9/7 wavelet with scalar quantization and the irreversible component transform, steered by <see cref="PdfJpxOptions.Quality"/>.</summary>
    Lossy = 1,
}
