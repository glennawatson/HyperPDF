// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Formats an existing typed requirement assessment in the current interface language.</summary>
public interface IRequirementText
{
    /// <summary>Gets the current translated summary.</summary>
    string Summary { get; }

    /// <summary>Gets the current translated unmet requirement details.</summary>
    IReadOnlyList<string> Details { get; }
}
