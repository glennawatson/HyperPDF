// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Separates normative expectations from recovery and unreviewed similarity.</summary>
internal enum ComparisonCategory
{
    /// <summary>Selected well-formed PDF regions have independent standards expectations.</summary>
    WellFormedStandards = 0,

    /// <summary>Malformed-file recovery is compared without a conformance claim.</summary>
    MalformedRecovery = 1,

    /// <summary>No independent expectations are available.</summary>
    Unreviewed = 2,
}
