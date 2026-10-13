// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Checks a page's trap network annotation against ISO 32000-2 §12.5.6.21 and §14.11.6.2 (ISO 32000-1 §14.11.6): one
/// network per page, last in <c>/Annots</c>, with <c>/AP</c>, <c>/AS</c> and <c>/F</c> holding only the Print and
/// ReadOnly flags, and either <c>/LastModified</c> or <c>/Version</c> with <c>/AnnotStates</c>. A <c>/Version</c> network
/// is out of date when the page names an object its array does not list, when a listed object is gone or was changed by
/// an edit, or when <c>/AnnotStates</c> no longer gives the other annotations' states in order. A <c>/LastModified</c>
/// network is out of date when the page's own date is later. For a page that was in the file when it was opened, an edit
/// to its contents, resources or annotation states also makes either kind out of date, as editors do not always update
/// the dates. A page added since opening, such as a copy, is checked by what its network records only.
/// </summary>
/// <remarks>
/// The <c>/Version</c> array may list more objects than the page names, such as resource dictionaries some writers add;
/// those are allowed as long as they still exist and were not changed.
/// </remarks>
internal static class PdfTrapNetworkValidator
{
    /// <summary>The only flags a trap network may have: Print and ReadOnly.</summary>
    internal const int RequiredFlags = (int)(PdfAnnotationFlags.Print | PdfAnnotationFlags.ReadOnly);

    /// <summary>Checks a page's trap network.</summary>
    /// <param name="page">The page.</param>
    /// <param name="edits">The document's edits since opening.</param>
    /// <returns>The problems found, and whether an edit touched the page.</returns>
    internal static PdfTrapNetworkCheck Check(PdfTrapNetworkPage page, PdfTrapNetworkEdits edits)
    {
        var network = page.Network;
        var description = new PdfPageDescription();
        PdfPageDescriptionReader.Collect(page.Store, page.Page, description);
        var problems = Structure(page, network) | Tracking(page, network, description, edits) | Edited(page, network, description, edits);
        return new(problems, IsTouched(page, network, description, edits));
    }

    /// <summary>Checks the network's count, position, flags and appearance.</summary>
    /// <param name="page">The page.</param>
    /// <param name="network">The network annotation.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Structure(PdfTrapNetworkPage page, PdfDictionary network)
    {
        var problems = page.NetworkCount > 1 ? PdfTrapNetworkProblems.MultipleNetworks : PdfTrapNetworkProblems.None;
        if (page.NetworkIndex != page.Annotations.Count - 1)
        {
            problems |= PdfTrapNetworkProblems.NotLast;
        }

        var flags = network.Get(KnownName.F);
        if (flags.Kind != PdfKind.Integer || flags.AsInteger() != RequiredFlags)
        {
            problems |= PdfTrapNetworkProblems.WrongFlags;
        }

        return problems | Appearance(network);
    }

    /// <summary>Checks that <c>/AS</c> picks a normal appearance stream.</summary>
    /// <param name="network">The network annotation.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Appearance(PdfDictionary network)
    {
        var normal = network.GetDictionary(KnownName.AP)?.Get(KnownName.N) ?? default;
        var state = network.GetName(KnownName.AS);
        if (normal.AsStream() is not null)
        {
            return state.IsNone ? PdfTrapNetworkProblems.MissingAppearanceState : PdfTrapNetworkProblems.None;
        }

        return normal.Kind == PdfKind.Dictionary && !state.IsNone && normal.AsDictionary()!.Get(state).AsStream() is not null
            ? PdfTrapNetworkProblems.None
            : PdfTrapNetworkProblems.MissingAppearance;
    }

    /// <summary>Checks the network against what it records about the page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="network">The network annotation.</param>
    /// <param name="description">The objects the page names.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Tracking(PdfTrapNetworkPage page, PdfDictionary network, PdfPageDescription description, PdfTrapNetworkEdits edits)
    {
        var dated = !network.GetRaw(KnownName.LastModified).IsNull;
        var version = network.Get(KnownName.Version);
        var states = network.Get(KnownName.AnnotStates);
        if (dated && version.IsNull && states.IsNull)
        {
            return Dates(page.Page, network);
        }

        return !dated && version.AsArray() is { } listed && states.AsArray() is { } recorded
            ? Versions(page.Store, listed, description, edits) | States(page, recorded)
            : PdfTrapNetworkProblems.MissingChangeTracking;
    }

    /// <summary>Compares the page's <c>/LastModified</c> with the network's.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="network">The network annotation.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Dates(PdfDictionary page, PdfDictionary network)
    {
        if (PdfDate.Parse(network.GetStringBytes(KnownName.LastModified)) is not { } made)
        {
            return PdfTrapNetworkProblems.MissingChangeTracking;
        }

        // A page date that cannot be read says nothing about the traps, so it is not held against them.
        return PdfDate.Parse(page.GetStringBytes(KnownName.LastModified)) is { } changed && changed > made
            ? PdfTrapNetworkProblems.PageNewer
            : PdfTrapNetworkProblems.None;
    }

    /// <summary>Checks a <c>/Version</c> array against the objects the page names.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="version">The array.</param>
    /// <param name="description">The objects the page names.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Versions(PdfObjectStore store, PdfArray version, PdfPageDescription description, PdfTrapNetworkEdits edits)
    {
        var listed = new HashSet<int>(version.Count);
        foreach (var item in version.Items)
        {
            if (!item.IsReference)
            {
                continue;
            }

            var number = item.AsReference().Number;
            _ = listed.Add(number);
            if (StoreReading.GetObject(store, item.AsReference()).IsNull || edits.Changed(number))
            {
                return PdfTrapNetworkProblems.ContentChanged;
            }
        }

        foreach (var number in description.Described)
        {
            if (!listed.Contains(number))
            {
                return PdfTrapNetworkProblems.ContentChanged;
            }
        }

        return PdfTrapNetworkProblems.None;
    }

    /// <summary>Checks <c>/AnnotStates</c> against the states of the page's other annotations, in order.</summary>
    /// <param name="page">The page.</param>
    /// <param name="recorded">The array.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems States(PdfTrapNetworkPage page, PdfArray recorded)
    {
        var position = 0;
        for (var i = 0; i < page.Annotations.Count; i++)
        {
            if (PdfTrapNetworkReader.Get(page.Store, page.Annotations, i) is not { } annotation || PdfTrapNetworkReader.IsNetwork(annotation))
            {
                continue;
            }

            if (position >= recorded.Count || !SameState(StoreReading.Resolve(page.Store, recorded.GetRaw(position)), annotation.GetName(KnownName.AS)))
            {
                return PdfTrapNetworkProblems.AnnotationsChanged;
            }

            position++;
        }

        return position == recorded.Count ? PdfTrapNetworkProblems.None : PdfTrapNetworkProblems.AnnotationsChanged;
    }

    /// <summary>Determines whether a recorded state matches an annotation's state.</summary>
    /// <param name="recorded">The <c>/AnnotStates</c> entry: a name, or null for an annotation without <c>/AS</c>.</param>
    /// <param name="state">The annotation's <c>/AS</c>, or no name.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool SameState(PdfValue recorded, PdfName state) =>
        recorded.IsNull ? state.IsNone : recorded.Kind == PdfKind.Name && recorded.AsName() == state;

    /// <summary>Checks what the document's edits changed on a page that was in the file when it was opened.</summary>
    /// <param name="page">The page.</param>
    /// <param name="network">The network annotation.</param>
    /// <param name="description">The objects the page names.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns>The problems.</returns>
    private static PdfTrapNetworkProblems Edited(PdfTrapNetworkPage page, PdfDictionary network, PdfPageDescription description, PdfTrapNetworkEdits edits)
    {
        if (page.Id.IsValid && edits.Touches(page.Id.Number) && edits.Original(page.Id.Number).IsNull)
        {
            return PdfTrapNetworkProblems.None;
        }

        var problems = ContentEdited(page, description, edits) ? PdfTrapNetworkProblems.ContentChanged : PdfTrapNetworkProblems.None;
        var dated = !network.GetRaw(KnownName.LastModified).IsNull;
        return dated && StatesEdited(page, edits) ? problems | PdfTrapNetworkProblems.AnnotationsChanged : problems;
    }

    /// <summary>Determines whether an edit changed the page's contents or resources.</summary>
    /// <param name="page">The page.</param>
    /// <param name="description">The objects the page names.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one did.</returns>
    private static bool ContentEdited(PdfTrapNetworkPage page, PdfPageDescription description, PdfTrapNetworkEdits edits)
    {
        if (page.Id.IsValid && edits.Touches(page.Id.Number))
        {
            var original = edits.Original(page.Id.Number).AsDictionary();
            if (original is null
                || !PdfValueEquality.KeyEqual(original, page.Page, KnownName.Contents)
                || !PdfValueEquality.KeyEqual(original, page.Page, KnownName.Resources))
            {
                return true;
            }
        }

        return AnyDiffers(description.Described, edits) || AnyDiffers(description.Containers, edits);
    }

    /// <summary>Determines whether any object of a set holds a value other than the file's.</summary>
    /// <param name="numbers">The object numbers.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool AnyDiffers(HashSet<int> numbers, PdfTrapNetworkEdits edits)
    {
        foreach (var number in numbers)
        {
            if (edits.Differs(number))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an edit changed the appearance states of the page's other annotations, or their order.</summary>
    /// <param name="page">The page.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one did.</returns>
    private static bool StatesEdited(PdfTrapNetworkPage page, PdfTrapNetworkEdits edits)
    {
        if (!AnnotationsTouched(page, edits))
        {
            return false;
        }

        var current = new List<PdfName>(page.Annotations.Count);
        PdfTrapNetworkReader.States(page.Store, page.Annotations, current);
        var original = new List<PdfName>(current.Count);
        OriginalStates(page, edits, original);
        return !CollectionsMarshal.AsSpan(current).SequenceEqual(CollectionsMarshal.AsSpan(original));
    }

    /// <summary>Lists the appearance states the page's other annotations had in the file.</summary>
    /// <param name="page">The page.</param>
    /// <param name="edits">The document's edits.</param>
    /// <param name="states">Receives the states.</param>
    private static void OriginalStates(PdfTrapNetworkPage page, PdfTrapNetworkEdits edits, List<PdfName> states)
    {
        var dictionary = page.Id.IsValid ? edits.Original(page.Id.Number).AsDictionary() : page.Page;
        if (dictionary is null || edits.ResolveOriginal(dictionary.GetRaw(KnownName.Annots)).AsArray() is not { } annotations)
        {
            return;
        }

        foreach (var item in annotations.Items)
        {
            if (edits.ResolveOriginal(item).AsDictionary() is { } annotation && !PdfTrapNetworkReader.IsNetwork(annotation))
            {
                states.Add(annotation.GetName(KnownName.AS));
            }
        }
    }

    /// <summary>Determines whether an edit touched the page, its <c>/Annots</c> array or one of its annotations.</summary>
    /// <param name="page">The page.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one did.</returns>
    private static bool AnnotationsTouched(PdfTrapNetworkPage page, PdfTrapNetworkEdits edits)
    {
        if ((page.Id.IsValid && edits.Touches(page.Id.Number)) || TouchesReference(page.Page.GetRaw(KnownName.Annots), edits))
        {
            return true;
        }

        foreach (var item in page.Annotations.Items)
        {
            if (TouchesReference(item, edits))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an edit touched the page or anything the network was made from.</summary>
    /// <param name="page">The page.</param>
    /// <param name="network">The network annotation.</param>
    /// <param name="description">The objects the page names.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one did.</returns>
    private static bool IsTouched(PdfTrapNetworkPage page, PdfDictionary network, PdfPageDescription description, PdfTrapNetworkEdits edits)
    {
        if (AnnotationsTouched(page, edits) || AnyTouched(description.Described, edits) || AnyTouched(description.Containers, edits))
        {
            return true;
        }

        if (network.Get(KnownName.Version).AsArray() is { } version)
        {
            foreach (var item in version.Items)
            {
                if (TouchesReference(item, edits))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Determines whether an edit touched any object of a set.</summary>
    /// <param name="numbers">The object numbers.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when one did.</returns>
    private static bool AnyTouched(HashSet<int> numbers, PdfTrapNetworkEdits edits)
    {
        foreach (var number in numbers)
        {
            if (edits.Touches(number))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a value is a reference to an object an edit touched.</summary>
    /// <param name="value">The value as written.</param>
    /// <param name="edits">The document's edits.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool TouchesReference(PdfValue value, PdfTrapNetworkEdits edits) => value.IsReference && edits.Touches(value.AsReference().Number);
}
