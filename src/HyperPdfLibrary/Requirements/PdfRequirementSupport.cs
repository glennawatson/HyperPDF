// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Requirements;

/// <summary>How much of a requirement type a viewer provides (ISO 32000-2 §12.11.2, Table 275).</summary>
public enum PdfRequirementSupport
{
    /// <summary>The viewer does not provide the feature.</summary>
    Unsupported = 0,

    /// <summary>The viewer provides part of the feature. The requirement is not fully met.</summary>
    Partial = 1,

    /// <summary>The viewer provides everything the requirement type names.</summary>
    Supported = 2,
}
