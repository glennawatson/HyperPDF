// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms;

namespace HyperPdf.ViewModels.FormFilling;

/// <summary>An option picked for a combo or list box.</summary>
/// <param name="Field">The field.</param>
/// <param name="Option">The option index.</param>
[DebuggerDisplay("FormChoice: {Field} option {Option}")]
public readonly record struct FormChoice(FormField Field, int Option);
