// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Json;

/// <summary>The property names of the JSON layout.</summary>
internal static class PdfJsonKeys
{
    /// <summary>The key of an object whose body is a value.</summary>
    internal const string Value = "value";

    /// <summary>The key of an object whose body is a stream.</summary>
    internal const string Stream = "stream";

    /// <summary>The key of a stream's dictionary.</summary>
    internal const string Dictionary = "dict";

    /// <summary>The key of a stream's data.</summary>
    internal const string Data = "data";

    /// <summary>The key of the trailer.</summary>
    internal const string Trailer = "trailer";

    /// <summary>The key of the array that holds the header and the object table.</summary>
    internal const string Qpdf = "qpdf";
}
