// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>One field's name and value in a form submission.</summary>
/// <param name="Name">The field's full name.</param>
/// <param name="Value">The field's value.</param>
[DebuggerDisplay("FormSubmittedField: {Name} = {Value}")]
public readonly record struct FormSubmittedField(string Name, string Value);
