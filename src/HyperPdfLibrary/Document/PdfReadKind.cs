// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>The reads whose results <see cref="PdfReadSnapshots"/> keeps.</summary>
internal enum PdfReadKind
{
    /// <summary>The article threads of the catalog.</summary>
    Threads = 0,

    /// <summary>The page-piece data of a dictionary.</summary>
    PieceInfo = 1,

    /// <summary>The web capture information.</summary>
    WebCapture = 2,

    /// <summary>The document part hierarchy.</summary>
    DocumentParts = 3,

    /// <summary>The multimedia annotations of the whole document.</summary>
    MultimediaAnnotations = 4,

    /// <summary>The XFA packets.</summary>
    Xfa = 5,

    /// <summary>The document-level scripts.</summary>
    DocumentScripts = 6,

    /// <summary>The portfolio.</summary>
    Portfolio = 7,

    /// <summary>The associated files of the whole document.</summary>
    AssociatedFiles = 8,

    /// <summary>The requirements of the catalog.</summary>
    Requirements = 9,

    /// <summary>The alternates of an image.</summary>
    ImageAlternates = 10,

    /// <summary>The reference XObject of a form.</summary>
    ReferenceXObject = 11,

    /// <summary>The document information.</summary>
    Info = 12,

    /// <summary>The document information combined with the XMP.</summary>
    ResolvedInfo = 13,

    /// <summary>The XMP metadata of a dictionary.</summary>
    Xmp = 14,

    /// <summary>The requirement evaluation of the catalog.</summary>
    RequirementAssessment = 15,

    /// <summary>The trap-network save guard completed for the current objects.</summary>
    TrapNetworksChecked = 16,

    /// <summary>The catalog's XMP metadata.</summary>
    CatalogXmp = 17,

    /// <summary>The number of kinds; keep last.</summary>
    Count = 18,
}
