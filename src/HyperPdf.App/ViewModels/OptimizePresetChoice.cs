// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Optimizing;

namespace HyperPdf.App.ViewModels;

/// <summary>A size choice offered in the optimised copy dialog.</summary>
/// <param name="Preset">The preset.</param>
/// <param name="Name">The short name.</param>
/// <param name="Description">What the choice does, in plain words.</param>
[DebuggerDisplay("OptimizePresetChoice: {Name}")]
public sealed record OptimizePresetChoice(OptimizePreset Preset, string Name, string Description);
