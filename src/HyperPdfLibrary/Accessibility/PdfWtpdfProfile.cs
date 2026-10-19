// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>A conformance level of Well-Tagged PDF (WTPDF 1.0, clause 6).</summary>
public enum PdfWtpdfProfile
{
    /// <summary>No level; not a valid claim.</summary>
    None = 0,

    /// <summary>
    /// Content that software can reuse, declared as <c>http://pdfa.org/declarations/wtpdf#reuse1.0</c>, the spelling veraPDF
    /// accepts. The spelling with a slash before the <c>#</c> is also read.
    /// </summary>
    Reuse = 1,

    /// <summary>Content that assistive technology can use, declared as <c>http://pdfa.org/declarations/wtpdf#accessibility1.0</c>. It adds PDF/UA-2 conformance to the reuse rules.</summary>
    Accessibility = 2,
}
