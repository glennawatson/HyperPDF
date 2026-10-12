// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Walks the interactive form's field tree for the PDF/A form and action rules.</summary>
internal static class PdfAFormFields
{
    /// <summary>
    /// Lists the form's fields, each once: every entry of <c>/Fields</c>, and the <c>/Kids</c> of a field that have a
    /// partial name (<c>/T</c>). A kid without a name is a widget, not a field, and its own kids are not walked; veraPDF
    /// reads the field tree the same way.
    /// </summary>
    /// <param name="context">The validation.</param>
    /// <returns>The fields, parents before their kids.</returns>
    internal static List<PdfDictionary> Read(PdfAValidationContext context)
    {
        var fields = new List<PdfDictionary>();
        if (context.Document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is not { } roots)
        {
            return fields;
        }

        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfDictionary>();
        Push(pending, roots, false);
        while (pending.TryPop(out var field))
        {
            if (!seen.Add(field))
            {
                continue;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            fields.Add(field);
            if (field.GetArray(KnownName.Kids) is { } kids)
            {
                Push(pending, kids, true);
            }
        }

        return fields;
    }

    /// <summary>Queues the field dictionaries of an array, last first so they come off the stack in order.</summary>
    /// <param name="pending">The fields still to visit.</param>
    /// <param name="list">The <c>/Fields</c> or <c>/Kids</c> array.</param>
    /// <param name="named">Whether only entries with a partial name are fields, as for <c>/Kids</c>.</param>
    private static void Push(Stack<PdfDictionary> pending, PdfArray list, bool named)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list.GetDictionary(i) is { } field && (!named || field.ContainsKey(KnownName.T)))
            {
                pending.Push(field);
            }
        }
    }
}
