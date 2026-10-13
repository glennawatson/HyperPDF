// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A button's on-state name and whether the button is on.</summary>
/// <param name="Name">The on-state name; <see langword="null"/> when the field is not a button.</param>
/// <param name="IsOn">Whether the button is on.</param>
[DebuggerDisplay("FormButtonOnState: {Name} {IsOn}")]
internal readonly record struct FormButtonOnState(string? Name, bool IsOn);
