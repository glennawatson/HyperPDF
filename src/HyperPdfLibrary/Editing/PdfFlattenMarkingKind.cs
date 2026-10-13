// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>How flattened content is marked (ISO 32000-2 14.6, 14.8.2.2).</summary>
internal enum PdfFlattenMarkingKind
{
    /// <summary>Not marked, as the document is not tagged.</summary>
    None = 0,

    /// <summary>Marked as an artifact, outside the logical structure.</summary>
    Artifact = 1,

    /// <summary>Marked with an id that a structure element owns.</summary>
    Content = 2,
}
