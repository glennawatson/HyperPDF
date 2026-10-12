// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Ocr;

/// <summary>Whether a text recogniser is ready, and if not, what is missing.</summary>
public enum OcrEngineStatus
{
    /// <summary>The recogniser and its language data are ready.</summary>
    Ready = 0,

    /// <summary>The native Tesseract library was not found on this computer.</summary>
    LibraryMissing = 1,

    /// <summary>The language data for one or more chosen languages was not found.</summary>
    LanguageMissing = 2,

    /// <summary>Tesseract was found but could not start, for example because it is too old or its data is damaged.</summary>
    StartFailed = 3,
}
