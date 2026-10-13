// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Writes the actions of a field as data: the additional-actions dictionary of the field and the activation action of a push button (ISO 32000-2 12.6.3 and 12.7.8).</summary>
internal static class FormFieldActionData
{
    /// <summary>Writes the field's additional-actions dictionary (<c>/AA</c>) with the keystroke, format, validate and calculate scripts.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The field dictionary.</param>
    /// <param name="definition">The definition.</param>
    internal static void WriteField(PdfObjectStore store, PdfDictionary target, PdfFieldDefinition definition)
    {
        if (definition.Actions is not { } actions)
        {
            return;
        }

        var additional = BuildAdditional(store, actions);
        if (additional.Count > 0)
        {
            target.Set(KnownName.AA, PdfValue.FromDictionary(additional));
        }
    }

    /// <summary>Writes the action a push button runs when clicked (<c>/A</c>).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The widget dictionary.</param>
    /// <param name="definition">The definition.</param>
    internal static void WriteWidget(PdfObjectStore store, PdfDictionary target, PdfFieldDefinition definition)
    {
        if (definition.Type == PdfFieldType.PushButton && BuildActivation(store, definition.Actions) is { } action)
        {
            target.Set(KnownName.A, PdfValue.FromDictionary(action));
        }
    }

    /// <summary>Builds the additional-actions dictionary for some scripts.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="actions">The actions.</param>
    /// <returns>The dictionary; empty when no script is set.</returns>
    internal static PdfDictionary BuildAdditional(PdfObjectStore store, PdfFieldActions actions)
    {
        var additional = new PdfDictionary(store);
        SetScript(store, additional, KnownName.K, actions.Keystroke);
        SetScript(store, additional, KnownName.F, actions.Format);
        SetScript(store, additional, KnownName.V, actions.Validate);
        SetScript(store, additional, KnownName.C, actions.Calculate);
        return additional;
    }

    /// <summary>Builds the action a push button runs.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="actions">The actions, or <see langword="null"/>.</param>
    /// <returns>A JavaScript action, a URI action, or <see langword="null"/> when neither is set.</returns>
    internal static PdfDictionary? BuildActivation(PdfObjectStore store, PdfFieldActions? actions)
    {
        if (actions?.Activate is { } script)
        {
            return BuildScript(store, script);
        }

        if (actions?.ActivateUri is not { Length: > 0 } uri)
        {
            return null;
        }

        var action = new PdfDictionary(store);
        action.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("Action"u8)));
        action.Set(KnownName.S, PdfValue.FromName(KnownName.URI));
        action.Set(KnownName.URI, PdfValue.FromString(Encoding.ASCII.GetBytes(uri)));
        return action;
    }

    /// <summary>Builds a JavaScript action.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="script">The script.</param>
    /// <returns>The action dictionary.</returns>
    internal static PdfDictionary BuildScript(PdfObjectStore store, string script)
    {
        var action = new PdfDictionary(store);
        action.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("Action"u8)));
        action.Set(KnownName.S, PdfValue.FromName(KnownName.JavaScript));
        action.Set(KnownName.JS, FormFieldBuilder.Text(store, script));
        return action;
    }

    /// <summary>Adds a script action to the additional-actions dictionary when there is a script.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="additional">The dictionary receiving the action.</param>
    /// <param name="key">The trigger's key.</param>
    /// <param name="script">The script, or <see langword="null"/>.</param>
    private static void SetScript(PdfObjectStore store, PdfDictionary additional, PdfName key, string? script)
    {
        if (script is not null)
        {
            additional.Set(key, PdfValue.FromDictionary(BuildScript(store, script)));
        }
    }
}
