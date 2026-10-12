// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>The independent reason for selecting or withholding a renderer reference.</summary>
internal enum ReferenceReason
{
    /// <summary>There is no independent oracle.</summary>
    NoIndependentOracle = 0,

    /// <summary>PDFium satisfies the oracle and retains the stated preference.</summary>
    PreferredStandardsCorrectPdfium = 1,

    /// <summary>Only pdf.js satisfies the independent checks.</summary>
    OnlyPdfJsSatisfiesOracle = 2,

    /// <summary>Neither available reference satisfies the independent checks.</summary>
    NeitherReferenceSatisfiesOracle = 3,

    /// <summary>Malformed recovery does not establish standards conformance.</summary>
    MalformedRecovery = 4,
}
