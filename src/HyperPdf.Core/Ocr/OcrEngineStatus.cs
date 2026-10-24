// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Ocr;

/// <summary>Whether a text recogniser is ready, and if not, what is missing.</summary>
public enum OcrEngineStatus
{
    /// <summary>The recogniser and its language data are ready.</summary>
    Ready = 0,

    /// <summary>The language data for one or more chosen languages was not found.</summary>
    LanguageMissing = 1,

    /// <summary>The language data was found but could not be used, for example because it is damaged or of a kind the recogniser does not read.</summary>
    StartFailed = 2,
}
