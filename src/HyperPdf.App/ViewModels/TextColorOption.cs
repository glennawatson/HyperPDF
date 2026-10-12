// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A named text colour offered by the text tool.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Color">The colour value.</param>
[DebuggerDisplay("{Name}: {Color}")]
public readonly record struct TextColorOption(string Name, uint Color);
