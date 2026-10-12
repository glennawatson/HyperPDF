// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on the blending colour space of pages that draw with transparency (ISO 19005-2/3 6.2.10, ISO 19005-4 6.2.9):
/// without a PDF/A output intent, such a page needs a <c>/Group</c> with a <c>/CS</c>.
/// </summary>
internal static class PdfAPageRules
{
    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.PageTransparencyColorSpace,
        PdfARule.PageTransparencyColorSpacePdfA4,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        if (PdfAOutputIntentColorSpace.Read(context.Document.Catalog) is not null)
        {
            return;
        }

        var document = context.Document;
        for (var index = 0; index < document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            if (page.Dictionary.GetDictionary(KnownName.Group) is { } group && group.ContainsKey(KnownName.CS))
            {
                continue;
            }

            if (!PdfATransparencyUse.PageUses(context, page))
            {
                continue;
            }

            var where = PdfAContexts.Page(index);
            context.Fail(PdfARule.PageTransparencyColorSpace, where);
            context.Require(PdfAOutputIntentColorSpace.Read(page.Dictionary) is not null, PdfARule.PageTransparencyColorSpacePdfA4, where);
        }
    }
}
