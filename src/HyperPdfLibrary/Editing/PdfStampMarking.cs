// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>How a stamped drawing is marked, so tagged documents keep it out of the logical structure (ISO 32000-2 14.8.2.2).</summary>
internal enum PdfStampMarking
{
    /// <summary>Not marked.</summary>
    None = 0,

    /// <summary>Marked as an artifact.</summary>
    Artifact = 1,

    /// <summary>Marked as a pagination artifact of subtype Watermark.</summary>
    Watermark = 2,
}
