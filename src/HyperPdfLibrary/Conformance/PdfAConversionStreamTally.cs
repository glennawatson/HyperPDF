// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>Counts what the stream fixes changed and found.</summary>
[DebuggerDisplay("PdfAConversionStreamTally: {Recompressed} recompressed")]
internal sealed class PdfAConversionStreamTally
{
    /// <summary>Gets or sets the LZW streams compressed again.</summary>
    internal int Recompressed { get; set; }

    /// <summary>Gets or sets the streams whose data lives in another file.</summary>
    internal int External { get; set; }

    /// <summary>Gets or sets the streams with a crypt filter.</summary>
    internal int Crypt { get; set; }

    /// <summary>Gets or sets the JPEG 2000 streams in a PDF/A-1 conversion.</summary>
    internal int Jpx { get; set; }
}
