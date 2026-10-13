// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>One choice of a combo box or list box.</summary>
/// <param name="Value">The export value.</param>
/// <param name="Label">The text shown; empty shows the value.</param>
[DebuggerDisplay("FormChoiceOption: {Value} {Label}")]
public readonly record struct FormChoiceOption(string Value, string Label);
