// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Refuses an edit that the document's certification signature does not allow. A certifying signature sets a DocMDP level
/// (ISO 32000-2 12.8.2.2): level 1 allows no change, level 2 allows form filling and signing, and level 3 also allows
/// annotations. Saving after a disallowed edit would make the certification fail, so the edit is not made.
/// </summary>
internal static class PdfEditPermission
{
    /// <summary>The level used when a DocMDP transform names none (ISO 32000-2 12.8.2.2).</summary>
    private const int DefaultLevel = 2;

    /// <summary>The highest level the standard defines.</summary>
    private const int HighestLevel = 3;

    /// <summary>Checks that the document's certification allows an edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="what">The edit in plain words, for the refusal, such as "change the bookmarks".</param>
    /// <exception cref="InvalidOperationException">The document is certified and its level does not allow the edit.</exception>
    internal static void Require(PdfDocument document, PdfChangeKinds kinds, string what)
    {
        var level = GetCertificationLevel(document);
        if (level == 0)
        {
            return;
        }

        var permission = (PdfMdpPermission)level;
        var change = new PdfObjectChange(0, ToModification(kinds), null, false);
        if (!PdfMdpEvaluator.IsPermitted(change, permission, []))
        {
            throw new InvalidOperationException(
                "This document is certified with a signature that does not allow you to " + what + " (permission level "
                + level.ToString(System.Globalization.CultureInfo.InvariantCulture) + "). The change was not made, as it would break the signature.");
        }
    }

    /// <summary>Reads the DocMDP level of the certifying signature.</summary>
    /// <param name="document">The document.</param>
    /// <returns>1, 2 or 3; 0 when the document is not certified.</returns>
    internal static int GetCertificationLevel(PdfDocument document)
    {
        var signature = document.Catalog.GetDictionary(KnownName.Perms)?.GetDictionary(KnownName.DocMDP);
        if (signature is null)
        {
            return 0;
        }

        var references = signature.GetArray(KnownName.Reference);
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } reference && reference.IsName(KnownName.TransformMethod, KnownName.DocMDP))
            {
                return Math.Clamp(reference.GetDictionary(KnownName.TransformParams)?.GetInt32(KnownName.P, DefaultLevel) ?? DefaultLevel, 1, HighestLevel);
            }
        }

        return DefaultLevel;
    }

    /// <summary>Converts the kinds an edit declares to the kinds the permission check knows.</summary>
    /// <param name="kinds">The edit's kinds.</param>
    /// <returns>The modification kinds.</returns>
    private static PdfModificationKinds ToModification(PdfChangeKinds kinds)
    {
        var result = PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.Annotations) != 0 ? PdfModificationKinds.Annotation : PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.FormFill) != 0 ? PdfModificationKinds.FormFill : PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.Signing) != 0 ? PdfModificationKinds.Signature : PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.PageChanges) != 0 ? PdfModificationKinds.Pages : PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.Metadata) != 0 ? PdfModificationKinds.Metadata : PdfModificationKinds.None;
        result |= (kinds & PdfChangeKinds.Other) != 0 ? PdfModificationKinds.Other : PdfModificationKinds.None;
        return result;
    }
}
