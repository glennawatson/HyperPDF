// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Ocr;

/// <summary>What happened when a page was put through text recognition.</summary>
public enum OcrPageStatus
{
    /// <summary>Words were found and written onto the page.</summary>
    Recognized = 0,

    /// <summary>The page already had text, so it was left alone.</summary>
    AlreadyHasText = 1,

    /// <summary>No words were found.</summary>
    NoTextFound = 2,

    /// <summary>The page could not be rendered, for example because the document closed.</summary>
    NotRendered = 3,

    /// <summary>The recogniser or its language data is not installed.</summary>
    Unavailable = 4,

    /// <summary>Words were found but read too poorly to trust, so nothing was written.</summary>
    Unsure = 5,
}
