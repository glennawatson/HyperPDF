// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Archiving;

/// <summary>A PDF/A level: a part of ISO 19005 and its conformance letter.</summary>
public enum ArchiveLevel
{
    /// <summary>PDF/A-1b: the page looks the same in the future.</summary>
    PdfA1B = 0,

    /// <summary>PDF/A-1a: as 1b, with a reading structure and Unicode text.</summary>
    PdfA1A = 1,

    /// <summary>PDF/A-2b: the page looks the same in the future; allows transparency and layers.</summary>
    PdfA2B = 2,

    /// <summary>PDF/A-2u: as 2b, and all text can be copied and searched.</summary>
    PdfA2U = 3,

    /// <summary>PDF/A-2a: as 2u, with a reading structure.</summary>
    PdfA2A = 4,

    /// <summary>PDF/A-3b: as 2b, and any kind of file may be attached.</summary>
    PdfA3B = 5,

    /// <summary>PDF/A-3u: as 3b, and all text can be copied and searched.</summary>
    PdfA3U = 6,

    /// <summary>PDF/A-3a: as 3u, with a reading structure.</summary>
    PdfA3A = 7,

    /// <summary>PDF/A-4: based on PDF 2.0.</summary>
    PdfA4 = 8,

    /// <summary>PDF/A-4e: PDF/A-4 for engineering, allowing 3D content.</summary>
    PdfA4E = 9,

    /// <summary>PDF/A-4f: PDF/A-4 allowing attached files of any kind.</summary>
    PdfA4F = 10,
}
