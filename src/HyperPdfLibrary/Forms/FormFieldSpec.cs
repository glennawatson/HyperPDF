// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A field definition after the library filled in what it left open: the full flags, the default appearance, the button states and the ordered options.</summary>
/// <param name="Definition">The definition.</param>
/// <param name="Partial">The field's partial name.</param>
/// <param name="Flags">The flags, including the ones the field's kind adds.</param>
/// <param name="DefaultAppearance">The default appearance string.</param>
/// <param name="Buttons">The states of a check box or radio group; <see langword="null"/> for other kinds.</param>
/// <param name="Options">The options, sorted when the sort flag is set.</param>
[DebuggerDisplay("FormFieldSpec: {Definition.Name}")]
internal sealed record FormFieldSpec(
    PdfFieldDefinition Definition,
    string Partial,
    PdfFieldFlags Flags,
    string DefaultAppearance,
    FormButtonStates? Buttons,
    PdfFieldOption[] Options)
{
    /// <summary>Plans a definition.</summary>
    /// <param name="definition">The definition, already checked.</param>
    /// <returns>The plan.</returns>
    internal static FormFieldSpec Create(PdfFieldDefinition definition)
    {
        var flags = definition.Flags | FormFieldFlags.GetTypeFlags(definition.Type);
        var options = (PdfFieldOption[])definition.Options.Clone();
        if ((flags & PdfFieldFlags.Sort) != 0)
        {
            Array.Sort(options, static (a, b) => string.Compare(a.Label.Length > 0 ? a.Label : a.Value, b.Label.Length > 0 ? b.Label : b.Value, StringComparison.OrdinalIgnoreCase));
        }

        return new(definition, FormFieldNames.GetLeaf(definition.Name), flags, FormFieldFonts.Build(definition), PlanButtons(definition), options);
    }

    /// <summary>Plans the button states of a check box or radio group.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The states; <see langword="null"/> for other kinds.</returns>
    private static FormButtonStates? PlanButtons(PdfFieldDefinition definition) => definition.Type switch
    {
        PdfFieldType.RadioButton => FormButtonStates.Plan(FormFieldArguments.GetRadioExports(definition)),
        PdfFieldType.CheckBox => FormButtonStates.Plan([definition.ExportValue ?? FormValues.DefaultOnState]),
        _ => null,
    };
}
