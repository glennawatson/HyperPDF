// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>What a document's declared requirements mean for the viewer (ISO 32000-2 §12.11).</summary>
/// <param name="Level">How far the requirements are met.</param>
/// <param name="Summary">The unmet features in plain words; empty when <paramref name="Level"/> is <see cref="RequirementLevel.None"/>.</param>
/// <param name="Details">One plain line for each unmet requirement.</param>
[DebuggerDisplay("RequirementReport: {Level}")]
public sealed record RequirementReport(RequirementLevel Level, string Summary, IReadOnlyList<string> Details)
{
    /// <summary>Gets the report of a document whose requirements are met or absent.</summary>
    public static RequirementReport None { get; } = new(RequirementLevel.None, string.Empty, []);

    /// <summary>Gets a value indicating whether the document shall not be processed further, so forms, buttons and actions stay off.</summary>
    public bool StopsProcessing => Level == RequirementLevel.Critical;
}
