// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Requirements;

/// <summary>What the document's requirements allow a viewer to do (ISO 32000-2 §12.11.3 and §12.11.6).</summary>
public enum PdfRequirementDecision
{
    /// <summary>Every requirement is met, or the document has none. Process the document normally.</summary>
    Satisfied = 0,

    /// <summary>
    /// Some requirements are unmet but their penalties add up to less than 100, so the document is acceptable. The viewer
    /// takes its normal fallback for each unsupported feature (§12.11.6).
    /// </summary>
    Fallback = 1,

    /// <summary>
    /// The unmet penalties add up to 100 or more, so the viewer cannot produce what the author intended and shall not
    /// continue to process the document (§12.11.6). A viewer may still show the pages as a static picture.
    /// </summary>
    Critical = 2,
}
