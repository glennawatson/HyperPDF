// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Protection;

namespace HyperPdf.App.ViewModels;

/// <summary>One choice of encryption in the protect window.</summary>
/// <param name="Format">The encryption.</param>
/// <param name="Name">The name shown.</param>
/// <param name="Description">What it means, in plain words.</param>
[DebuggerDisplay("ProtectionFormatChoice: {Name}")]
public sealed record ProtectionFormatChoice(ProtectionFormat Format, string Name, string Description)
{
    /// <inheritdoc/>
    public override string ToString() => Name;
}
