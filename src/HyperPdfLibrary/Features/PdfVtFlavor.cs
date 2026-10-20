// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Features;

/// <summary>The PDF/VT flavor a file claims (ISO 16612-2).</summary>
public enum PdfVtFlavor
{
    /// <summary>The file makes no PDF/VT claim.</summary>
    None = 0,

    /// <summary>PDF/VT-1: a single self-contained file with all resources embedded.</summary>
    Vt1 = 1,

    /// <summary>PDF/VT-2: a file that may refer to external resources through a PDF/X-4p style reference.</summary>
    Vt2 = 2,

    /// <summary>PDF/VT-2s: a PDF/VT-2 file that is one part of a stream of files.</summary>
    Vt2s = 3,

    /// <summary>The file names a PDF/VT version this library does not know.</summary>
    Unknown = 4,
}
