// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Ocr;

/// <summary>Recognises text in a page image. An engine is used by one thread at a time.</summary>
public interface IOcrEngine : IDisposable
{
    /// <summary>Gets a value indicating whether the engine and its language data were found.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets whether the engine is ready, and if not, what is missing.</summary>
    OcrEngineStatus Status { get; }

    /// <summary>Recognises the words in an 8 bit greyscale image.</summary>
    /// <param name="image">The image, one byte per pixel.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="pixelsPerPoint">The image resolution, used to report word positions in points.</param>
    /// <param name="output">The list receiving the words.</param>
    void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output);
}
