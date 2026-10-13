// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Reports how saving pending edits affects each signature field, as Acrobat judges modifications: an incremental save
/// keeps signed bytes intact; DocMDP level 1 allows no change, level 2 allows form filling and signing, level 3 also
/// annotations; FieldMDP (a signature's /Reference or its field's /Lock) locks named fields, and /Lock /P adds a level.
/// This follows ISO 32000-2 12.8.2 and Acrobat's categories.
/// </summary>
internal static class PdfSignatureEffects
{
    /// <summary>DocMDP level 1: no changes.</summary>
    private const int NoChanges = 1;

    /// <summary>DocMDP level 2: form filling and signing; also the default when /P is missing.</summary>
    private const int FormFilling = 2;

    /// <summary>DocMDP level 3: form filling, signing and annotations.</summary>
    private const int Annotating = 3;

    /// <summary>The deepest field hierarchy walked.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>Builds the report.</summary>
    /// <param name="document">The document with pending edits.</param>
    /// <param name="incremental">Whether the save appends an update; a full rewrite always breaks signatures.</param>
    /// <returns>The report.</returns>
    internal static PdfSignatureReport Analyze(PdfDocument document, bool incremental)
    {
        var changes = PdfChangeClassifier.Classify(document);
        var fields = new List<SignatureField>();
        var form = document.Catalog.GetDictionary(KnownName.AcroForm);
        HashSet<PdfDictionary> visited = [with(ReferenceEqualityComparer.Instance)];
        CollectSignatureFields(form?.GetArray(KnownName.Fields), 0, visited, fields);
        var certifying = document.Catalog.GetDictionary(KnownName.Perms)?.GetRaw(KnownName.DocMDP).AsReference().Number ?? 0;
        var effects = new PdfSignatureEffect[fields.Count];
        for (var i = 0; i < effects.Length; i++)
        {
            effects[i] = AnalyzeField(document.Objects, changes, fields[i], certifying, incremental);
        }

        return new(changes.Kinds, [.. changes.ChangedFields], effects);
    }

    /// <summary>Gets the kinds a DocMDP level allows.</summary>
    /// <param name="permission">The level, or 0 for none.</param>
    /// <returns>The allowed kinds.</returns>
    internal static PdfChangeKinds Allowed(int permission) => permission switch
    {
        0 => (PdfChangeKinds)(-1),
        NoChanges => PdfChangeKinds.None,
        FormFilling => PdfChangeKinds.FormFill | PdfChangeKinds.Signing,
        _ => PdfChangeKinds.FormFill | PdfChangeKinds.Signing | PdfChangeKinds.Annotations,
    };

    /// <summary>Finds the terminal signature fields under a list of fields.</summary>
    /// <param name="kids">The /Fields or /Kids array.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="visited">The fields already visited.</param>
    /// <param name="output">Receives each signature field.</param>
    private static void CollectSignatureFields(PdfArray? kids, int depth, HashSet<PdfDictionary> visited, List<SignatureField> output)
    {
        for (var i = 0; kids is not null && depth < MaxFieldDepth && i < kids.Count; i++)
        {
            if (kids.GetDictionary(i) is not { } field || !visited.Add(field))
            {
                continue;
            }

            if (HasFieldKids(field))
            {
                CollectSignatureFields(field.GetArray(KnownName.Kids), depth + 1, visited, output);
            }
            else if (PdfChangeClassifier.FieldType(field).Is(KnownName.Sig))
            {
                output.Add(new(kids.GetRaw(i).AsReference(), field));
            }
        }
    }

    /// <summary>Determines whether a field has child fields rather than only widgets.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when a kid has a /T.</returns>
    private static bool HasFieldKids(PdfDictionary field)
    {
        var kids = field.GetArray(KnownName.Kids);
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            if (kids.GetDictionary(i)?.ContainsKey(KnownName.T) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Works out the effect on one signature field.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="changes">The classified edits.</param>
    /// <param name="signature">The field.</param>
    /// <param name="certifying">The object number of the certifying signature, or zero.</param>
    /// <param name="incremental">Whether the save is incremental.</param>
    /// <returns>The effect.</returns>
    private static PdfSignatureEffect AnalyzeField(PdfObjectStore store, PdfChangeClassifier changes, SignatureField signature, int certifying, bool incremental)
    {
        var field = signature.Field;
        var name = PdfChangeClassifier.FullName(field);
        var valueRaw = field.GetRaw(KnownName.V);
        var value = StoreReading.Resolve(store, valueRaw).AsDictionary();
        var signed = value?.ContainsKey(KnownName.Contents) == true;
        var touched = SignatureTouched(store, changes, signature, valueRaw);
        var docMdp = ReadDocMdp(value, signed && valueRaw.IsReference && valueRaw.AsReference().Number == certifying);
        var lockDictionary = field.GetDictionary(KnownName.Lock);
        var lockLevel = signed ? lockDictionary?.GetInt32(KnownName.P, 0) ?? 0 : 0;
        var permission = Strictest(docMdp, lockLevel);
        var locked = signed ? LockedChanges(store, value!, lockDictionary, changes.ChangedFields, name) : [];
        return new(name, signed, docMdp != 0, permission, incremental && signed && !touched, changes.Kinds & ~Allowed(permission), locked);
    }

    /// <summary>Determines whether the edits change a field's signature value or its signature dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="changes">The classified edits.</param>
    /// <param name="signature">The field.</param>
    /// <param name="valueRaw">The field's /V as stored.</param>
    /// <returns><see langword="true"/> when the signature itself was changed.</returns>
    private static bool SignatureTouched(PdfObjectStore store, PdfChangeClassifier changes, SignatureField signature, PdfValue valueRaw)
    {
        if (valueRaw.IsReference && changes.ChangedObjects.Contains(valueRaw.AsReference().Number))
        {
            return true;
        }

        return signature.Id.IsValid
        && changes.ChangedObjects.Contains(signature.Id.Number)
        && !PdfValueEquality.KeyEqual(
        StoreTransactions.GetOriginal(
        store,
        signature.Id.Number).AsDictionary(),
        signature.Field,
        KnownName.V);
    }

    /// <summary>Reads a signature's DocMDP level from its /Reference transforms.</summary>
    /// <param name="signature">The signature dictionary.</param>
    /// <param name="certifying">Whether the catalog's /Perms names it, which makes it certifying even without a transform.</param>
    /// <returns>The level, or 0 when it does not certify.</returns>
    private static int ReadDocMdp(PdfDictionary? signature, bool certifying)
    {
        var references = signature?.GetArray(KnownName.Reference);
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } reference && reference.IsName(KnownName.TransformMethod, KnownName.DocMDP))
            {
                return Math.Clamp(reference.GetDictionary(KnownName.TransformParams)?.GetInt32(KnownName.P, FormFilling) ?? FormFilling, NoChanges, Annotating);
            }
        }

        return certifying ? FormFilling : 0;
    }

    /// <summary>Gets the stricter of two levels, where 0 means none.</summary>
    /// <param name="first">The first level.</param>
    /// <param name="second">The second level.</param>
    /// <returns>The stricter level.</returns>
    private static int Strictest(int first, int second) => first == 0 || second == 0 ? Math.Max(first, second) : Math.Clamp(Math.Min(first, second), NoChanges, Annotating);

    /// <summary>Lists the changed fields that a signature's FieldMDP transform or its field's /Lock locks.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="signature">The signature dictionary.</param>
    /// <param name="lockDictionary">The field's /Lock, if any.</param>
    /// <param name="changedFields">The changed fields.</param>
    /// <param name="ownName">The signature field's own name, which signing it may change.</param>
    /// <returns>The locked fields that changed.</returns>
    private static string[] LockedChanges(PdfObjectStore store, PdfDictionary signature, PdfDictionary? lockDictionary, List<string> changedFields, string ownName)
    {
        var rules = new List<PdfDictionary>();
        if (lockDictionary is not null)
        {
            rules.Add(lockDictionary);
        }

        var references = signature.GetArray(KnownName.Reference);
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } reference
        && store.Names.NameEquals(
        reference.GetName(KnownName.TransformMethod),
        "FieldMDP"u8) && reference.GetDictionary(KnownName.TransformParams) is { } parameters)
            {
                rules.Add(parameters);
            }
        }

        var locked = new List<string>();
        foreach (var changed in changedFields)
        {
            if (!string.Equals(changed, ownName, StringComparison.Ordinal) && IsLockedByAny(store, rules, changed))
            {
                locked.Add(changed);
            }
        }

        return [.. locked];
    }

    /// <summary>Determines whether any FieldMDP rule locks a field.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="rules">The rules: /Lock or FieldMDP transform parameters.</param>
    /// <param name="field">The field's fully qualified name.</param>
    /// <returns><see langword="true"/> when locked.</returns>
    private static bool IsLockedByAny(PdfObjectStore store, List<PdfDictionary> rules, string field)
    {
        var actionKey = store.Names.Intern("Action"u8);
        foreach (var rule in rules)
        {
            var action = rule.GetName(actionKey);
            if (action.IsNone)
            {
                continue;
            }

            var spelling = store.Names.GetSpelling(action);
            var listed = Lists(rule.GetArray(KnownName.Fields), field);
            if (spelling.SequenceEqual("All"u8) || (spelling.SequenceEqual("Include"u8) && listed) || (spelling.SequenceEqual("Exclude"u8) && !listed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a field, or one of its ancestors, is named in a /Fields list.</summary>
    /// <param name="fields">The list of fully qualified names.</param>
    /// <param name="field">The field's fully qualified name.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool Lists(PdfArray? fields, string field)
    {
        for (var i = 0; fields is not null && i < fields.Count; i++)
        {
            var name = PdfText.Decode(fields.Get(i).AsStringBytes());
            if (string.Equals(name, field, StringComparison.Ordinal) || (field.Length > name.Length && field.StartsWith(name, StringComparison.Ordinal) && field[name.Length] == '.'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A terminal signature field.</summary>
    /// <param name="Id">The field's object id, or an invalid id when direct.</param>
    /// <param name="Field">The field dictionary.</param>
    [DebuggerDisplay("SignatureField: {Id}")]
    private readonly record struct SignatureField(PdfObjectId Id, PdfDictionary Field);
}
