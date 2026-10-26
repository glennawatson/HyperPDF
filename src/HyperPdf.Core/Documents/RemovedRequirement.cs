// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>Retains a removed requirement so its notice follows interface language changes.</summary>
/// <param name="Type">The document's requirement identifier, preserved as data.</param>
/// <param name="TitleKey">The known capability's resource key, or null for an unknown document identifier.</param>
[DebuggerDisplay("RemovedRequirement: {Type}")]
public sealed record RemovedRequirement(string Type, string? TitleKey)
{
    /// <summary>Gets the notice in the current interface language.</summary>
    public string Text => UiText.Format(
        "The file listed \"{0}\" as something it needs. The edits left nothing in the file that uses it, so the requirement was removed.",
        TitleKey is null ? Type : UiText.Get(TitleKey));
}
