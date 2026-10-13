// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Flattening;

namespace HyperPdf.App.ViewModels;

/// <summary>What to flatten, as offered in the flattened copy window.</summary>
/// <param name="Scope">The scope.</param>
/// <param name="Name">The short name.</param>
/// <param name="Description">What the choice means, in plain words.</param>
[DebuggerDisplay("FlattenScopeChoice: {Name}")]
public sealed record FlattenScopeChoice(FlattenCopyScope Scope, string Name, string Description);
