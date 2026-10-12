// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>PDF/A rules on interactive form fields (ISO 19005-1 6.6.2, ISO 19005-2/3 6.4.1).</summary>
internal static class PdfAFormRules
{
    /// <summary>The report context of a form field that is a direct object.</summary>
    private const string FieldContext = "form field";

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules => [PdfARule.FormFieldAdditionalActions];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        foreach (var field in PdfAFormFields.Read(context))
        {
            context.Require(!field.ContainsKey(KnownName.AA), PdfARule.FormFieldAdditionalActions, FieldContext);
        }
    }
}
