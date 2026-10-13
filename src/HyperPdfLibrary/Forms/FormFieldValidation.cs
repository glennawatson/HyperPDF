// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Checks field definitions before anything is changed, so a refused definition leaves the document as it was. Bad
/// arguments throw <see cref="ArgumentException"/>; a name that clashes, or a definition a conformance claim does not
/// allow, throws <see cref="InvalidOperationException"/> with a plain reason.
/// </summary>
internal static class FormFieldValidation
{
    /// <summary>Checks definitions and reserves their names in the index, so two definitions cannot take one name.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The fields the form has.</param>
    /// <param name="definitions">The definitions.</param>
    /// <exception cref="ArgumentException">A definition is not valid.</exception>
    /// <exception cref="InvalidOperationException">A name clashes or the document's conformance claim forbids the definition.</exception>
    internal static void Check(PdfDocument document, FormFieldIndex index, IReadOnlyList<PdfFieldDefinition> definitions)
    {
        var pdfa = PdfStampConformance.GetPdfAPart(document);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            FormFieldArguments.Check(document, definition);
            CheckName(index, definition);
            CheckConformance(document, definition, pdfa);
        }
    }

    /// <summary>Refuses what a PDF/A claim forbids.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="pdfa">The part of ISO 19005 the document claims, or 0.</param>
    /// <exception cref="InvalidOperationException">The claim would break.</exception>
    internal static void CheckConformance(PdfDocument document, PdfFieldDefinition definition, int pdfa)
    {
        if (pdfa == 0)
        {
            return;
        }

        if (!definition.GenerateAppearances)
        {
            throw new InvalidOperationException("The document claims PDF/A, which does not allow the form to ask readers to draw fields (NeedAppearances). The library has to draw the field.");
        }

        if (definition.Actions is { IsEmpty: false })
        {
            throw new InvalidOperationException("The document claims PDF/A, which does not allow actions on form fields.");
        }

        if (NeedsFont(definition) && !FormFieldFonts.IsEmbeddedInForm(document, definition.DefaultAppearance))
        {
            throw new InvalidOperationException(
                "The document claims PDF/A, which needs every font embedded. Give a default appearance (DefaultAppearance) that names an embedded font from the form's default resources.");
        }
    }

    /// <summary>Checks the field's name against the fields that exist, and reserves it.</summary>
    /// <param name="index">The fields the form has and those reserved.</param>
    /// <param name="definition">The definition.</param>
    /// <exception cref="InvalidOperationException">The name is used, or a field above it holds a value.</exception>
    private static void CheckName(FormFieldIndex index, PdfFieldDefinition definition)
    {
        var name = definition.Name;
        var parent = FormFieldNames.GetParent(name);
        while (parent.Length > 0)
        {
            ReserveGroup(index, name, parent);
            parent = FormFieldNames.GetParent(parent);
        }

        if (index.GetUse(name) != FormFieldIndex.NameUse.Free)
        {
            throw new InvalidOperationException($"A field named '{name}' already exists. Field names must be unique in a form.");
        }

        index.Reserve(name, FormFieldIndex.NameUse.Terminal);
    }

    /// <summary>Checks that a name above a new field can group fields, and reserves it.</summary>
    /// <param name="index">The fields the form has and those reserved.</param>
    /// <param name="name">The new field's name.</param>
    /// <param name="parent">The name above it.</param>
    /// <exception cref="InvalidOperationException">The name above belongs to a field that holds a value.</exception>
    private static void ReserveGroup(FormFieldIndex index, string name, string parent)
    {
        var use = index.GetUse(parent);
        if (use == FormFieldIndex.NameUse.Terminal)
        {
            throw new InvalidOperationException($"The field '{name}' cannot be placed under '{parent}', because '{parent}' is a field that holds a value.");
        }

        if (use == FormFieldIndex.NameUse.Free)
        {
            index.Reserve(parent, FormFieldIndex.NameUse.Group);
        }
    }

    /// <summary>Determines whether a field's appearance draws text.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns><see langword="true"/> when the appearance needs a font.</returns>
    private static bool NeedsFont(PdfFieldDefinition definition) =>
        definition.Type is PdfFieldType.Text or PdfFieldType.ComboBox or PdfFieldType.ListBox
        || (definition.Type is PdfFieldType.PushButton && !string.IsNullOrEmpty(definition.Caption));
}
