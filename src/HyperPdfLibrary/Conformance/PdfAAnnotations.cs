// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Finds the annotations the PDF/A annotation and action rules check, and names them in reports.</summary>
internal static class PdfAAnnotations
{
    /// <summary>Lists the annotations of every page's <c>/Annots</c> array, in page order.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>The annotations.</returns>
    internal static List<PdfAAnnotation> Read(PdfAValidationContext context)
    {
        var document = context.Document;
        var annotations = new List<PdfAAnnotation>();
        for (var index = 0; index < document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (PdfDocumentPages.GetPage(document, index).Dictionary.GetArray(KnownName.Annots) is not { } list)
            {
                continue;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list.GetDictionary(i) is { } annotation)
                {
                    annotations.Add(new(annotation, index, list.GetRaw(i).AsReference().Number));
                }
            }
        }

        return annotations;
    }

    /// <summary>Names an annotation for the report: its object, or its page when it is a direct object.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>For example "object 12".</returns>
    internal static string Describe(PdfAAnnotation annotation) =>
        annotation.Number > 0 ? PdfAContexts.Object(annotation.Number) : PdfAContexts.Page(annotation.Page);

    /// <summary>Records a failure against an annotation when a condition does not hold.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="holds">Whether the rule holds.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="annotation">The annotation.</param>
    internal static void Require(PdfAValidationContext context, bool holds, PdfARule rule, PdfAAnnotation annotation)
    {
        if (!holds)
        {
            context.Fail(rule, Describe(annotation));
        }
    }

    /// <summary>Interns a list of name spellings.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="spellings">The spellings.</param>
    /// <returns>The names.</returns>
    internal static HashSet<PdfName> Intern(PdfNameTable names, string[] spellings)
    {
        var set = new HashSet<PdfName>(spellings.Length);
        foreach (var spelling in spellings)
        {
            _ = set.Add(names.Intern(spelling));
        }

        return set;
    }
}
