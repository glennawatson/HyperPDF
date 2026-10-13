// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The kind of an artifact, content that is not part of the document's structure (ISO 32000-2 14.8.2.2).</summary>
public enum PdfArtifactKind
{
    /// <summary>Decoration with no further meaning, such as a rule or a background.</summary>
    Layout = 0,

    /// <summary>A page header, footer or similar running content.</summary>
    Pagination = 1,

    /// <summary>A watermark or other print-only overlay.</summary>
    Background = 2,
}
