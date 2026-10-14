// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>Whether the optimiser may re-encode black-and-white images as JBIG2.</summary>
public enum PdfJbig2Mode
{
    /// <summary>Black-and-white images are not coded as JBIG2.</summary>
    Off = 0,

    /// <summary>
    /// Black-and-white images are coded as JBIG2 when that is smaller than the other lossless codings. Every pixel is
    /// kept. PDF/A-1 files never get JBIG2, because that standard forbids it.
    /// </summary>
    Lossless = 1,

    /// <summary>
    /// LOSSY: like <see cref="Lossless"/>, and text-like images may also be coded with lossy symbol merging, which
    /// replaces similar-looking marks by one shared shape and can swap one character for another. It applies only when
    /// <see cref="PdfOptimizeOptions.AllowLossyImages"/> is on, and never to images used as masks.
    /// </summary>
    Lossy = 2,
}
