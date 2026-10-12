// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Removes the actions a PDF/A level forbids: action types outside the level's list, named actions other than page
/// navigation, actions on widgets, and additional actions (ISO 19005-1 §6.6; ISO 19005-2 and -3 §6.4.1 and §6.5;
/// ISO 19005-4:2020 §6.4.1 and §6.6). Links and outline entries stay; only their forbidden actions go.
/// </summary>
internal static class PdfAConversionActions
{
    /// <summary>The additional-action triggers PDF/A-4 allows outside form fields (ISO 19005-4:2020 §6.6.3).</summary>
    private static readonly string[] Pdf4Triggers = ["E", "X", "D", "U", "Fo", "Bl"];

    /// <summary>Applies the cleanup.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var changed = PdfAConversionRewriter.Rewrite(session, 0, static (s, _, dictionary) => Fix(s, dictionary));
        if (changed > 0)
        {
            session.Changed(
                "Removed actions PDF/A does not allow, such as scripts, sounds, launching programs and actions on form fields.",
                PdfARule.ActionTypePdfA2,
                PdfARule.ActionTypePdfA1,
                PdfARule.ActionTypePdfA4,
                PdfARule.ActionTypePdfA4E);
        }
    }

    /// <summary>Removes a dictionary's forbidden actions.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>A changed copy, or <see langword="null"/>.</returns>
    private static PdfDictionary? Fix(PdfAConversionSession session, PdfDictionary dictionary)
    {
        PdfDictionary? copy = null;
        var widget = dictionary.IsName(KnownName.Subtype, KnownName.Widget);
        var field = dictionary.ContainsKey(KnownName.FT) || (dictionary.ContainsKey(KnownName.T) && dictionary.ContainsKey(KnownName.Kids));
        RemoveWhen(dictionary, KnownName.A, dictionary.ContainsKey(KnownName.A) && (widget || !IsAllowed(session, dictionary.Get(KnownName.A))), ref copy);
        var open = dictionary.Get(KnownName.OpenAction);
        RemoveWhen(dictionary, KnownName.OpenAction, open.AsDictionary() is not null && !IsAllowed(session, open), ref copy);
        FixNext(session, dictionary, ref copy);
        FixAdditional(session, dictionary, widget || field, ref copy);
        return copy;
    }

    /// <summary>Removes an entry when a condition holds.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="remove">Whether to remove it.</param>
    /// <param name="copy">The copy being made.</param>
    private static void RemoveWhen(PdfDictionary dictionary, KnownName key, bool remove, ref PdfDictionary? copy)
    {
        if (!remove)
        {
            return;
        }

        copy ??= dictionary.Clone();
        _ = copy.Remove(key);
    }

    /// <summary>Removes forbidden actions from an action's /Next chain.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy being made.</param>
    private static void FixNext(PdfAConversionSession session, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        if (!dictionary.ContainsKey(KnownName.S) || !dictionary.ContainsKey(KnownName.Next))
        {
            return;
        }

        var next = dictionary.Get(KnownName.Next);
        if (next.AsArray() is { } chain)
        {
            var kept = new PdfArray(session.Store, chain.Count);
            for (var i = 0; i < chain.Count; i++)
            {
                if (IsAllowed(session, chain.Get(i)))
                {
                    kept.Add(chain.GetRaw(i));
                }
            }

            if (kept.Count != chain.Count)
            {
                copy ??= dictionary.Clone();
                copy.Set(KnownName.Next, PdfValue.FromArray(kept));
            }
        }
        else if (!IsAllowed(session, next))
        {
            copy ??= dictionary.Clone();
            _ = copy.Remove(KnownName.Next);
        }
    }

    /// <summary>
    /// Removes additional actions: all of them in parts 1 to 3; in part 4, form fields keep theirs and other objects keep
    /// only the annotation triggers the part allows. Forbidden actions inside kept entries are removed too.
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="formField">Whether the dictionary is a widget or form field.</param>
    /// <param name="copy">The copy being made.</param>
    private static void FixAdditional(PdfAConversionSession session, PdfDictionary dictionary, bool formField, ref PdfDictionary? copy)
    {
        if (StoreReading.Resolve(session.Store, dictionary.GetRaw(KnownName.AA)).AsDictionary() is not { } actions)
        {
            return;
        }

        if (session.Part < PdfAClaim.Part4)
        {
            copy ??= dictionary.Clone();
            _ = copy.Remove(KnownName.AA);
            return;
        }

        var kept = KeptTriggers(session, actions, formField);
        if (kept.Count == actions.Count)
        {
            return;
        }

        copy ??= dictionary.Clone();
        if (kept.Count == 0)
        {
            _ = copy.Remove(KnownName.AA);
        }
        else
        {
            copy.Set(KnownName.AA, PdfValue.FromDictionary(kept));
        }
    }

    /// <summary>Keeps the additional actions PDF/A-4 allows.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="actions">The additional-actions dictionary.</param>
    /// <param name="formField">Whether it belongs to a widget or form field, which may keep any trigger.</param>
    /// <returns>The kept entries.</returns>
    private static PdfDictionary KeptTriggers(PdfAConversionSession session, PdfDictionary actions, bool formField)
    {
        var kept = new PdfDictionary(session.Store, actions.Count);
        for (var i = 0; i < actions.Count; i++)
        {
            var trigger = session.Store.Names.GetString(actions.GetKeyAt(i));
            if ((formField || Pdf4Triggers.Contains(trigger)) && IsAllowed(session, actions.GetValueAt(i)))
            {
                kept.Set(actions.GetKeyAt(i), actions.GetValueAt(i));
            }
        }

        return kept;
    }

    /// <summary>Determines whether an action is allowed at the level; a value that is not an action is left alone.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="value">The action value.</param>
    /// <returns><see langword="true"/> when it may stay.</returns>
    private static bool IsAllowed(PdfAConversionSession session, PdfValue value)
    {
        // Structure elements use /A for attributes, which have no /S; only action dictionaries are judged.
        if (StoreReading.Resolve(session.Store, value).AsDictionary() is not { } action || !action.ContainsKey(KnownName.S))
        {
            return true;
        }

        var type = session.Store.Names.GetString(action.GetName(KnownName.S));
        return type == "Named"
            ? session.Store.Names.GetString(action.GetName(KnownName.N)) is "NextPage" or "PrevPage" or "FirstPage" or "LastPage"
            : IsAllowedType(session, type);
    }

    /// <summary>Determines whether the level allows an action type (ISO 19005-1 §6.6.1; ISO 19005-2 §6.5.1; ISO 19005-4:2020 §6.6.1).</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="type">The action's /S.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsAllowedType(PdfAConversionSession session, string type) => type switch
    {
        "GoTo" or "GoToR" or "Thread" or "URI" or "SubmitForm" => true,
        "GoToE" => session.Part > PdfAClaim.Part1,
        "JavaScript" or "RichMediaExecute" or "GoToDp" => session.Part == PdfAClaim.Part4,
        "SetOCGState" or "GoTo3DView" => session.Level == PdfALevel.PdfA4E,
        _ => false,
    };
}
