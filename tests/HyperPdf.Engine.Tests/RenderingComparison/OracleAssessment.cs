// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Whether HyperPDF satisfies the supplied independent checks, without claiming full-file conformance.</summary>
internal enum OracleAssessment
{
    /// <summary>No independent oracle exists.</summary>
    Unresolved = 0,

    /// <summary>Every supplied check passes.</summary>
    Satisfied = 1,

    /// <summary>At least one supplied check fails.</summary>
    Violated = 2,
}
