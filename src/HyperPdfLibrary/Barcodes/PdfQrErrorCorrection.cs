// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Barcodes;

/// <summary>How much damage a QR Code can take and still be read; higher levels hold less text.</summary>
public enum PdfQrErrorCorrection
{
    /// <summary>About 7 percent of the code can be lost.</summary>
    Low = 0,

    /// <summary>About 15 percent of the code can be lost.</summary>
    Medium = 1,

    /// <summary>About 25 percent of the code can be lost.</summary>
    Quartile = 2,

    /// <summary>About 30 percent of the code can be lost.</summary>
    High = 3,
}
