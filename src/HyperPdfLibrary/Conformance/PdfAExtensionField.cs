// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A field of a PDF/A extension schema description and the rule that checks it.</summary>
/// <param name="Name">The field's local name.</param>
/// <param name="Rule">The rule.</param>
/// <param name="Form">What the field must be.</param>
internal readonly record struct PdfAExtensionField(string Name, PdfARule Rule, PdfAExtensionFieldForm Form);
