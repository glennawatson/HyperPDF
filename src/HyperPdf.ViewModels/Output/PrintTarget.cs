// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Output;

/// <summary>An entry in the print preview's destination list.</summary>
/// <param name="Kind">Where the print goes.</param>
/// <param name="Name">The printer's queue name, or an empty string.</param>
/// <param name="LabelKey">The name shown.</param>
[DebuggerDisplay("PrintTarget: {Label}")]
public sealed record PrintTarget(PrintDestination Kind, string Name, string LabelKey)
{
    /// <summary>Gets the localized display text.</summary>
    public string Label => Kind == PrintDestination.Printer ? LabelKey : UiText.Get(LabelKey);
}
