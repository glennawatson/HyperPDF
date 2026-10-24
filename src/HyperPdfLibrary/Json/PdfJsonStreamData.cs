// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Json;

/// <summary>What a JSON export puts in for the data of a stream.</summary>
public enum PdfJsonStreamData
{
    /// <summary>No data: only each stream's dictionary.</summary>
    None = 0,

    /// <summary>The data as it is stored in the file, base64 encoded, with the dictionary unchanged. A stream the file encrypts is exported decoded instead.</summary>
    Stored = 1,

    /// <summary>
    /// The data with its general filters undone (Flate, LZW, ASCII85 and the like), base64 encoded, and the dictionary without
    /// <c>/Filter</c>, <c>/DecodeParms</c> and <c>/Length</c>. A stream that ends in an image codec keeps its stored data.
    /// </summary>
    Decoded = 2,
}
