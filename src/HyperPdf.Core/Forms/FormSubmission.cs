// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>Form data that a form's submit button would send. Nothing is sent unless the user agrees.</summary>
/// <param name="Url">The address the form names, or <see langword="null"/>.</param>
/// <param name="Fields">The fields that would be sent.</param>
[DebuggerDisplay("FormSubmission: {Url}, {Fields.Count} fields")]
public sealed record FormSubmission(string? Url, IReadOnlyList<FormSubmittedField> Fields);
