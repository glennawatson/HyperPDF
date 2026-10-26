// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Protection;

namespace HyperPdf.ViewModels.Security;

/// <summary>One choice of encryption in the protect window.</summary>
/// <param name="Format">The encryption.</param>
/// <param name="NameKey">The name shown.</param>
/// <param name="DescriptionKey">What it means, in plain words.</param>
[DebuggerDisplay("ProtectionFormatChoice: {Name}")]
public sealed record ProtectionFormatChoice(ProtectionFormat Format, string NameKey, string DescriptionKey)
{
    /// <summary>Gets the localized display text.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the localized display text.</summary>
    public string Description => UiText.Get(DescriptionKey);

    /// <inheritdoc/>
    public override string ToString() => Name;
}
