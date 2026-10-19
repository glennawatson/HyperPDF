// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>Lists and edits the tags of an open document through the library.</summary>
internal sealed class HyperPdfTagEditorService : ITagEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTagEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfTagEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public bool IsTagged
    {
        get
        {
            using var access = HyperPdfNavigation.EnterPageRead(_owner);
            ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
            return PdfDocumentTagged.GetStructureTree(_owner.Document) is not null;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<DocumentTag> GetTags()
    {
        using var access = HyperPdfNavigation.EnterPageRead(_owner);
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        if (PdfDocumentTagged.GetStructureTree(_owner.Document) is not { } tree)
        {
            return [];
        }

        var tags = new List<DocumentTag>(tree.ElementCount);
        var pending = new Stack<PdfStructureElement>(tree.Roots.Count);
        for (var i = tree.Roots.Count - 1; i >= 0; i--)
        {
            pending.Push(tree.Roots[i]);
        }

        while (pending.TryPop(out var element))
        {
            tags.Add(ToTag(element));
            var kids = element.Kids;
            for (var i = kids.Count - 1; i >= 0; i--)
            {
                if (kids[i].Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        return tags;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DocumentRoleMapping> GetRoleMap()
    {
        using var access = HyperPdfNavigation.EnterPageRead(_owner);
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        var map = PdfDocumentStructureEditing.GetRoleMap(_owner.Document);
        var result = new DocumentRoleMapping[map.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(map[i].Type, map[i].MapsTo);
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetStandardTypes()
    {
        using var access = HyperPdfNavigation.EnterPageRead(_owner);
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        var is20 = _owner.Document.Objects.Version.StartsWith('2');
        return PdfStructureTypes.GetStandardNames(is20 ? PdfStructureTypes.Pdf20Namespace : PdfStructureTypes.Pdf17Namespace).ToArray();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetRoleMappingAsync(string type, string mapsTo, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new RoleStep(type, mapsTo),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetRoleMapping(document, step.Type, step.MapsTo),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> RemoveRoleMappingAsync(string type, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new RoleStep(type, string.Empty),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.RemoveRoleMapping(document, step.Type),
            cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<DocumentTagProblem> GetProblems(CancellationToken cancellationToken)
    {
        var report = HyperPdfAccessibility.GetAccessibilityReport(_owner, cancellationToken);
        var problems = new List<DocumentTagProblem>(report.Findings.Count);
        foreach (var finding in report.Findings)
        {
            problems.Add(new(finding.Message, finding.PageIndex, finding.ElementId?.Number ?? 0));
        }

        return problems;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetTypeChoices(int id)
    {
        using var access = HyperPdfNavigation.EnterPageRead(_owner);
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        return Find(_owner.Document, id) is { } element ? PdfDocumentStructureEditing.GetTypeChoices(_owner.Document, element) : [];
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetTypeAsync(int id, string type, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new TypeStep(id, type),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetType(document, Require(document, step.Id), step.Type),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetTextAsync(int id, DocumentTagField field, string? text, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new TextStep(id, field, text),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetText(document, Require(document, step.Id), Map(step.Field), step.Text),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetTableScopeAsync(int id, DocumentTableScope scope, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new ScopeStep(id, scope),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetTableScope(document, Require(document, step.Id), (PdfTableScope)step.Scope),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> MoveAsync(int id, int offset, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new MoveStep(id, offset),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) =>
            {
                if (!PdfDocumentStructureEditing.Move(document, Require(document, step.Id), step.Offset))
                {
                    throw new InvalidOperationException("The tag is already at the end of its group.");
                }
            },
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> NestAsync(int id, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new IdStep(id),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.Nest(document, Require(document, step.Id)),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetCellSpansAsync(int id, int rows, int columns, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new SpanStep(id, rows, columns),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetCellSpans(document, Require(document, step.Id), step.Rows, step.Columns),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetListNumberingAsync(int id, string? numbering, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new NumberingStep(id, numbering),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetListNumbering(document, Require(document, step.Id), step.Numbering),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> SetPlacementAsync(int id, string? placement, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new PlacementStep(id, placement),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.SetPlacement(document, Require(document, step.Id), step.Placement),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> MoveIntoAsync(int id, int newParentId, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new MoveIntoStep(id, newParentId),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.MoveInto(document, Require(document, step.Id), Require(document, step.NewParentId)),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> UnnestAsync(int id, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new IdStep(id),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.Unnest(document, Require(document, step.Id)),
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentEditResult> RemoveAsync(int id, CancellationToken cancellationToken) =>
        HyperPdfDocumentEdits.RunAsync(
            _owner,
            new IdStep(id),
            HyperPdfEditPrefetch.TagsAsync,
            static (document, step) => PdfDocumentStructureEditing.Remove(document, Require(document, step.Id)),
            cancellationToken);

    /// <summary>Describes an element for the app.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The tag.</returns>
    private static DocumentTag ToTag(PdfStructureElement element)
    {
        var children = 0;
        foreach (var kid in element.Kids)
        {
            if (kid.Element is not null)
            {
                children++;
            }
        }

        return new(
            element.Id.Number,
            element.RawType,
            element.Depth,
            element.PageIndex,
            element.AlternateText,
            element.ActualText,
            element.Expansion,
            element.Title,
            element.Dictionary.GetText(HyperPdfLibrary.Objects.KnownName.Lang),
            element.Kids.Count - children,
            children,
            string.Equals(element.MappedType, "TH", StringComparison.Ordinal),
            (DocumentTableScope)element.Attributes.Scope,
            element.MappedType is "TH" or "TD",
            element.Attributes.RowSpan,
            element.Attributes.ColumnSpan,
            string.Equals(element.MappedType, "L", StringComparison.Ordinal),
            element.Attributes.ListNumbering,
            element.Attributes.Placement);
    }

    /// <summary>Maps the app's text field to the library's.</summary>
    /// <param name="field">The app's field.</param>
    /// <returns>The library's field.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The field is not a known text.</exception>
    private static PdfStructureTextField Map(DocumentTagField field) => field switch
    {
        DocumentTagField.AlternateText => PdfStructureTextField.AlternateText,
        DocumentTagField.ActualText => PdfStructureTextField.ActualText,
        DocumentTagField.Expansion => PdfStructureTextField.Expansion,
        DocumentTagField.Title => PdfStructureTextField.Title,
        DocumentTagField.Language => PdfStructureTextField.Language,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Not a known text."),
    };

    /// <summary>Finds the element with an object number, which a refusal reports when it is gone.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentException">The document has no such tag.</exception>
    private static PdfStructureElement Require(PdfDocument document, int number) =>
        Find(document, number) ?? throw new ArgumentException("That tag is no longer in the document.", nameof(number));

    /// <summary>Finds the element with an object number.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    private static PdfStructureElement? Find(PdfDocument document, int number)
    {
        if (number <= 0 || PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return null;
        }

        var pending = new Stack<PdfStructureElement>(tree.Roots);
        while (pending.TryPop(out var element))
        {
            if (element.Id.Number == number)
            {
                return element;
            }

            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        return null;
    }

    /// <summary>The arguments of an edit that names one tag.</summary>
    /// <param name="Id">The tag.</param>
    private readonly record struct IdStep(int Id);

    /// <summary>The arguments of a type change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Type">The new type.</param>
    private readonly record struct TypeStep(int Id, string Type);

    /// <summary>The arguments of a text change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Field">The text.</param>
    /// <param name="Text">The new text.</param>
    private readonly record struct TextStep(int Id, DocumentTagField Field, string? Text);

    /// <summary>The arguments of a role map change.</summary>
    /// <param name="Type">The custom type.</param>
    /// <param name="MapsTo">The standard type; empty for a removal.</param>
    private readonly record struct RoleStep(string Type, string MapsTo);

    /// <summary>The arguments of a span change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Rows">The rows it covers.</param>
    /// <param name="Columns">The columns it covers.</param>
    private readonly record struct SpanStep(int Id, int Rows, int Columns);

    /// <summary>The arguments of a list numbering change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Numbering">The new numbering.</param>
    private readonly record struct NumberingStep(int Id, string? Numbering);

    /// <summary>The arguments of a placement change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Placement">The new placement.</param>
    private readonly record struct PlacementStep(int Id, string? Placement);

    /// <summary>The arguments of a move into another tag.</summary>
    /// <param name="Id">The tag to move.</param>
    /// <param name="NewParentId">The tag that takes it in.</param>
    private readonly record struct MoveIntoStep(int Id, int NewParentId);

    /// <summary>The arguments of a scope change.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Scope">The new scope.</param>
    private readonly record struct ScopeStep(int Id, DocumentTableScope Scope);

    /// <summary>The arguments of a move.</summary>
    /// <param name="Id">The tag.</param>
    /// <param name="Offset">The distance.</param>
    private readonly record struct MoveStep(int Id, int Offset);
}
