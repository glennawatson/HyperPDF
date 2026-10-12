// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A named line or character spacing offered by the text tool.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Value">The spacing in points or as a line-height multiplier.</param>
[DebuggerDisplay("{Name}: {Value}")]
public readonly record struct LineSpacingOption(string Name, float Value);
