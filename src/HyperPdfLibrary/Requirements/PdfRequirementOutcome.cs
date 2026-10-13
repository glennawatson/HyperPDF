// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Requirements;

/// <summary>What checking one requirement dictionary found.</summary>
public enum PdfRequirementOutcome
{
    /// <summary>The viewer provides the feature at the version asked for.</summary>
    Met = 0,

    /// <summary>The viewer does not provide the feature.</summary>
    NotSupported = 1,

    /// <summary>The viewer provides part of the feature only.</summary>
    PartlySupported = 2,

    /// <summary>The viewer provides the feature, but not at the version the requirement asks for.</summary>
    VersionNotSupported = 3,

    /// <summary>The requirement type is not one the viewer knows, so it cannot check it.</summary>
    UnknownType = 4,
}
