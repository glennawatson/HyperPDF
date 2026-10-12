// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on actions and trigger events (ISO 19005-1 6.6, ISO 19005-2/3 6.5, ISO 19005-4 6.6). Actions are found where
/// a viewer runs them: the catalog's open action and additional actions, page and annotation actions, form field additional
/// actions and outline items, each with its <c>/Next</c> chain.
/// </summary>
internal static class PdfAActionRules
{
    /// <summary>The report context of an action reached from an outline item.</summary>
    private const string OutlineContext = "outline";

    /// <summary>The report context of an action reached from a form field.</summary>
    private const string FieldContext = "form field";

    /// <summary>The action types PDF/A-1 allows.</summary>
    private static readonly string[] TypesPdfA1 = ["GoTo", "GoToR", "Thread", "URI", "Named", "SubmitForm"];

    /// <summary>The action types PDF/A-2 and PDF/A-3 allow.</summary>
    private static readonly string[] TypesPdfA2 = ["GoTo", "GoToR", "GoToE", "Thread", "URI", "Named", "SubmitForm"];

    /// <summary>The action types PDF/A-4 allows.</summary>
    private static readonly string[] TypesPdfA4 =
        ["GoTo", "GoToR", "GoToE", "Thread", "URI", "Named", "SubmitForm", "JavaScript", "RichMediaExecute", "GoToDp"];

    /// <summary>The action types PDF/A-4e adds to <see cref="TypesPdfA4"/>.</summary>
    private static readonly string[] TypesPdfA4E = ["SetOCGState", "GoTo3DView"];

    /// <summary>The named actions every part allows.</summary>
    private static readonly string[] NamedActions = ["NextPage", "PrevPage", "FirstPage", "LastPage"];

    /// <summary>The trigger events PDF/A-4 allows in additional actions outside widgets and form fields.</summary>
    private static readonly string[] Triggers = ["E", "X", "D", "U", "Fo", "Bl"];

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.ActionTypePdfA1,
        PdfARule.ActionTypePdfA2,
        PdfARule.ActionTypePdfA4,
        PdfARule.ActionTypePdfA4E,
        PdfARule.NamedAction,
        PdfARule.AdditionalActionsTriggers,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var names = context.Document.Objects.Names;
        var types = new PdfAActionTypes(
            PdfAAnnotations.Intern(names, TypesPdfA1),
            PdfAAnnotations.Intern(names, TypesPdfA2),
            PdfAAnnotations.Intern(names, TypesPdfA4),
            PdfAAnnotations.Intern(names, TypesPdfA4E),
            PdfAAnnotations.Intern(names, NamedActions),
            PdfAAnnotations.Intern(names, Triggers));
        var scope = new PdfAActionScope(context, types, [with(ReferenceEqualityComparer.Instance)]);
        var catalog = context.Document.Catalog;
        if (catalog.Get(KnownName.OpenAction) is { Kind: PdfKind.Dictionary } open)
        {
            CheckChain(scope, open.AsDictionary()!, PdfAContexts.Catalog);
        }

        CheckAdditional(scope, catalog, true, PdfAContexts.Catalog);
        CheckPages(scope);
        foreach (var field in PdfAFormFields.Read(context))
        {
            CheckAdditional(scope, field, false, FieldContext);
        }

        CheckOutlines(scope, catalog.GetDictionary(KnownName.Outlines));
    }

    /// <summary>Checks the additional actions of each page and the actions of each page's annotations.</summary>
    /// <param name="scope">The walk.</param>
    private static void CheckPages(PdfAActionScope scope)
    {
        var document = scope.Context.Document;
        for (var index = 0; index < document.PageCount; index++)
        {
            CheckAdditional(scope, PdfDocumentPages.GetPage(document, index).Dictionary, true, PdfAContexts.Page(index));
        }

        foreach (var annotation in PdfAAnnotations.Read(scope.Context))
        {
            var dictionary = annotation.Dictionary;
            var where = PdfAAnnotations.Describe(annotation);
            if (dictionary.Get(KnownName.A) is { Kind: PdfKind.Dictionary } action)
            {
                CheckChain(scope, action.AsDictionary()!, where);
            }

            CheckAdditional(scope, dictionary, !dictionary.IsName(KnownName.Subtype, KnownName.Widget), where);
        }
    }

    /// <summary>Checks an additional-actions dictionary: its trigger events and each action.</summary>
    /// <param name="scope">The walk.</param>
    /// <param name="owner">The dictionary holding <c>/AA</c>.</param>
    /// <param name="restricted">Whether PDF/A-4 limits its triggers; widgets and form fields are not limited.</param>
    /// <param name="where">The report context.</param>
    private static void CheckAdditional(PdfAActionScope scope, PdfDictionary owner, bool restricted, string where)
    {
        if (owner.Get(KnownName.AA) is not { Kind: PdfKind.Dictionary } value || value.AsDictionary() is not { } actions)
        {
            return;
        }

        for (var i = 0; i < actions.Count; i++)
        {
            if (restricted && !scope.Types.Triggers.Contains(actions.GetKeyAt(i)))
            {
                scope.Context.Fail(PdfARule.AdditionalActionsTriggers, where);
            }

            if (StoreReading.Resolve(scope.Context.Document.Objects, actions.GetValueAt(i)) is { Kind: PdfKind.Dictionary } action)
            {
                CheckChain(scope, action.AsDictionary()!, where);
            }
        }
    }

    /// <summary>Checks an action and the actions its <c>/Next</c> entries chain to.</summary>
    /// <param name="scope">The walk.</param>
    /// <param name="first">The first action.</param>
    /// <param name="where">The report context.</param>
    private static void CheckChain(PdfAActionScope scope, PdfDictionary first, string where)
    {
        var pending = new Stack<PdfDictionary>();
        pending.Push(first);
        while (pending.TryPop(out var action))
        {
            if (!scope.Visited.Add(action))
            {
                continue;
            }

            CheckAction(scope, action, where);
            var next = action.Get(KnownName.Next);
            if (next.Kind == PdfKind.Dictionary)
            {
                pending.Push(next.AsDictionary()!);
            }
            else if (next.AsArray() is { } list)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    PushAction(pending, list.Get(i));
                }
            }
        }
    }

    /// <summary>Queues an entry of a <c>/Next</c> array when it is an action dictionary.</summary>
    /// <param name="pending">The actions still to check.</param>
    /// <param name="value">The entry.</param>
    private static void PushAction(Stack<PdfDictionary> pending, PdfValue value)
    {
        if (value.Kind == PdfKind.Dictionary)
        {
            pending.Push(value.AsDictionary()!);
        }
    }

    /// <summary>Checks one action's type, and the name of a named action.</summary>
    /// <param name="scope">The walk.</param>
    /// <param name="action">The action dictionary.</param>
    /// <param name="where">The report context.</param>
    private static void CheckAction(PdfAActionScope scope, PdfDictionary action, string where)
    {
        var context = scope.Context;
        var types = scope.Types;
        var type = action.GetName(KnownName.S);
        var allowedPdfA4 = types.PdfA4.Contains(type);
        context.Require(types.PdfA1.Contains(type), PdfARule.ActionTypePdfA1, where);
        context.Require(types.PdfA2.Contains(type), PdfARule.ActionTypePdfA2, where);
        context.Require(allowedPdfA4, PdfARule.ActionTypePdfA4, where);
        context.Require(allowedPdfA4 || types.PdfA4E.Contains(type), PdfARule.ActionTypePdfA4E, where);
        if (type.Is(KnownName.Named))
        {
            context.Require(types.NamedActions.Contains(action.GetName(KnownName.N)), PdfARule.NamedAction, where);
        }
    }

    /// <summary>Checks the action of every outline item.</summary>
    /// <param name="scope">The walk.</param>
    /// <param name="outlines">The outline dictionary, or <see langword="null"/>.</param>
    private static void CheckOutlines(PdfAActionScope scope, PdfDictionary? outlines)
    {
        if (outlines?.GetDictionary(KnownName.First) is not { } first)
        {
            return;
        }

        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfDictionary>();
        pending.Push(first);
        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item))
            {
                continue;
            }

            scope.Context.CancellationToken.ThrowIfCancellationRequested();
            if (item.Get(KnownName.A) is { Kind: PdfKind.Dictionary } action)
            {
                CheckChain(scope, action.AsDictionary()!, OutlineContext);
            }

            PushItem(pending, item.GetDictionary(KnownName.Next));
            PushItem(pending, item.GetDictionary(KnownName.First));
        }
    }

    /// <summary>Queues an outline item when there is one.</summary>
    /// <param name="pending">The items still to visit.</param>
    /// <param name="item">The item, or <see langword="null"/>.</param>
    private static void PushItem(Stack<PdfDictionary> pending, PdfDictionary? item)
    {
        if (item is not null)
        {
            pending.Push(item);
        }
    }
}
