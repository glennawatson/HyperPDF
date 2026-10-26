// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// The tags of one open document: the list a screen reader walks, what a person should look at, and the form that changes
/// the chosen tag. Each change is one step in the document's undo history, so the page edit Undo also takes it back. What
/// each change did, or why it was refused, is written in <see cref="Status"/> for screen readers.
/// </summary>
[DebuggerDisplay("TagsViewModel: {Items.Count} tags, {Status}")]
public sealed partial class TagsViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The number of texts the form edits.</summary>
    private const int EditableTexts = 4;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Cancelled when the tab closes, which stops an edit or check in flight; its source is owned by <see cref="_subscriptions"/>.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>Owns the selection subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether the chosen tag can be changed.</summary>
    private readonly IObservable<bool> _canChange;

    /// <summary>Whether the chosen tag can move earlier among its siblings.</summary>
    private readonly IObservable<bool> _canMoveUp;

    /// <summary>Whether the chosen tag can move later among its siblings.</summary>
    private readonly IObservable<bool> _canMoveDown;

    /// <summary>Whether the chosen tag can be removed.</summary>
    private readonly IObservable<bool> _canRemove;

    /// <summary>Whether the chosen tag can go inside the tag above it.</summary>
    private readonly IObservable<bool> _canNest;

    /// <summary>Whether the chosen tag can move out of its parent.</summary>
    private readonly IObservable<bool> _canUnnest;

    /// <summary>Whether the chosen tag can move into the chosen target.</summary>
    private readonly IObservable<bool> _canMoveInto;

    /// <summary>The custom types of <see cref="RoleMappings"/>, in the same order.</summary>
    private string[] _roleTypes = [];

    /// <summary>The object numbers of <see cref="MoveTargets"/>, in the same order.</summary>
    private int[] _moveTargetIds = [];

    /// <summary>The retained status resource.</summary>
    private LocalizedMessage _status;

    /// <summary>The retained check summary resource.</summary>
    private LocalizedMessage _problemSummary = LocalizedMessage.Get(UiText, "The check has not run yet.");

    /// <summary>Whether the list is being made again, so choosing the tag that stays chosen does not move the reader.</summary>
    private bool _refreshing;

    /// <summary>Initializes a new instance of the <see cref="TagsViewModel"/> class.</summary>
    /// <param name="owner">The tab holding the document.</param>
    public TagsViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        _canChange = this.WhenChanged(static vm => vm.CanChange);
        _canMoveUp = this.WhenChanged(static vm => vm.CanMoveUp);
        _canMoveDown = this.WhenChanged(static vm => vm.CanMoveDown);
        _canRemove = this.WhenChanged(static vm => vm.CanRemove);
        _canNest = this.WhenChanged(static vm => vm.CanNest);
        _canUnnest = this.WhenChanged(static vm => vm.CanUnnest);
        _canMoveInto = this.WhenChanged(static vm => vm.CanMoveInto);
        var lifetime = new CancellationDisposable();
        _lifetime = lifetime.Token;
        _subscriptions.Add(lifetime);
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedItem).Skip(1).SubscribeSafe(_ => ChooseSelected(), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static vm => vm.MoveTargetIndex).SubscribeSafe(_ => CanMoveInto = CanChange && MoveTargetIndex >= 0, ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedProblem).Where(static problem => problem is not null).SubscribeSafe(_ => ShowProblem(), ErrorTrace.Write));
        _subscriptions.Add(owner.WhenChanged(static tab => tab.IsLoaded).Where(static loaded => loaded).SubscribeSafe(_ => Refresh(), ErrorTrace.Write));

        // An undo or redo of any document edit may have changed the tags.
        _subscriptions.Add(owner.Pages.WhenChanged(static pages => pages.StructureVersion).Skip(1).SubscribeSafe(_ => Refresh(), ErrorTrace.Write));
    }

    /// <summary>Gets the tags in reading order.</summary>
    [Reactive]
    public partial IReadOnlyList<TagItemViewModel> Items { get; private set; } = [];

    /// <summary>Gets what the latest check found.</summary>
    [Reactive]
    public partial IReadOnlyList<TagProblemItem> Problems { get; private set; } = [];

    /// <summary>Gets or sets the chosen tag.</summary>
    [Reactive]
    public partial TagItemViewModel? SelectedItem { get; set; }

    /// <summary>Gets or sets the chosen problem, which chooses the tag it is about and shows its page.</summary>
    [Reactive]
    public partial TagProblemItem? SelectedProblem { get; set; }

    /// <summary>Gets the types the chosen tag may take.</summary>
    [Reactive]
    public partial IReadOnlyList<string> TypeChoices { get; private set; } = [];

    /// <summary>Gets or sets the type shown in the form.</summary>
    [Reactive]
    public partial string? SelectedType { get; set; }

    /// <summary>Gets the scopes a table header cell can have, in the order of <see cref="DocumentTableScope"/>.</summary>
    public string[] ScopeChoices { get; } = [NotSet, "Row", "Column", "Both"];

    /// <summary>Gets the tags the chosen tag can move into: every tag except itself and the tags inside it.</summary>
    [Reactive]
    public partial IReadOnlyList<string> MoveTargets { get; private set; } = [];

    /// <summary>Gets or sets the position in <see cref="MoveTargets"/> of the tag to move the chosen tag into; -1 for none.</summary>
    [Reactive]
    public partial int MoveTargetIndex { get; set; } = -1;

    /// <summary>Gets a value indicating whether the chosen tag can move into the chosen target.</summary>
    [Reactive]
    public partial bool CanMoveInto { get; private set; }

    /// <summary>Gets how a list can label its items; the first entry means not set, and the rest are the names of ISO 32000-2 Table 383.</summary>
    public string[] ListNumberingChoices { get; } =
        [NotSet, "None", "Unordered", "Ordered", "Disc", "Circle", "Square", "Decimal", "UpperRoman", "LowerRoman", "UpperAlpha", "LowerAlpha", "Description"];

    /// <summary>Gets how a tag can sit in the flow of content; the first entry means not set, and the rest are the names of ISO 32000-2 Table 380.</summary>
    public string[] PlacementChoices { get; } = [NotSet, "Block", "Inline", "Before", "Start", "End"];

    /// <summary>Gets a value indicating whether the chosen tag can be given a placement.</summary>
    [Reactive]
    public partial bool CanChangePlacement { get; private set; }

    /// <summary>Gets or sets the placement shown in the form: how the chosen tag sits in the flow of content.</summary>
    [Reactive]
    public partial string? SelectedPlacement { get; set; }

    /// <summary>Gets a value indicating whether the chosen tag is a table cell, which can cover several rows and columns.</summary>
    [Reactive]
    public partial bool IsCell { get; private set; }

    /// <summary>Gets or sets how many rows the chosen cell covers, as typed.</summary>
    [Reactive]
    public partial string RowSpanText { get; set; } = "1";

    /// <summary>Gets or sets how many columns the chosen cell covers, as typed.</summary>
    [Reactive]
    public partial string ColumnSpanText { get; set; } = "1";

    /// <summary>Gets a value indicating whether the chosen tag is a list, which has a numbering.</summary>
    [Reactive]
    public partial bool IsList { get; private set; }

    /// <summary>Gets or sets the numbering shown in the form: how the chosen list labels its items.</summary>
    [Reactive]
    public partial string? SelectedListNumbering { get; set; }

    /// <summary>Gets a value indicating whether the chosen tag is a table header cell, which has a scope.</summary>
    [Reactive]
    public partial bool IsHeaderCell { get; private set; }

    /// <summary>Gets or sets the scope shown in the form: which cells the chosen header cell labels.</summary>
    [Reactive]
    public partial string? SelectedScope { get; set; }

    /// <summary>Gets or sets the description a screen reader says in place of the chosen tag's content.</summary>
    [Reactive]
    public partial string AlternateText { get; set; } = string.Empty;

    /// <summary>Gets or sets the text that replaces the chosen tag's content when it is read or copied.</summary>
    [Reactive]
    public partial string ActualText { get; set; } = string.Empty;

    /// <summary>Gets or sets the language of the chosen tag, such as en-AU.</summary>
    [Reactive]
    public partial string Language { get; set; } = string.Empty;

    /// <summary>Gets or sets the title of the chosen tag.</summary>
    [Reactive]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the document has tags.</summary>
    [Reactive]
    public partial bool IsTagged { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can be changed.</summary>
    [Reactive]
    public partial bool CanChange { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can move earlier among its siblings.</summary>
    [Reactive]
    public partial bool CanMoveUp { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can move later among its siblings.</summary>
    [Reactive]
    public partial bool CanMoveDown { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can be removed.</summary>
    [Reactive]
    public partial bool CanRemove { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can go inside the tag above it.</summary>
    [Reactive]
    public partial bool CanNest { get; private set; }

    /// <summary>Gets a value indicating whether the chosen tag can move out of its parent.</summary>
    [Reactive]
    public partial bool CanUnnest { get; private set; }

    /// <summary>Gets the custom tag types and what they stand for, one line each.</summary>
    [Reactive]
    public partial IReadOnlyList<string> RoleMappings { get; private set; } = [];

    /// <summary>Gets or sets the position in <see cref="RoleMappings"/> of the chosen mapping; -1 for none.</summary>
    [Reactive]
    public partial int RoleMappingIndex { get; set; } = -1;

    /// <summary>Gets the standard types a custom type can stand for.</summary>
    [Reactive]
    public partial IReadOnlyList<string> StandardTypes { get; private set; } = [];

    /// <summary>Gets or sets the name of the custom tag type to add.</summary>
    [Reactive]
    public partial string NewRoleType { get; set; } = string.Empty;

    /// <summary>Gets or sets the standard type the new custom type stands for.</summary>
    [Reactive]
    public partial string? NewRoleTarget { get; set; }

    /// <summary>Gets the sentence that says what the check found, or that it has not run.</summary>
    public string ProblemSummary => _problemSummary.Value ?? string.Empty;

    /// <summary>Gets what the latest change did or why it was refused.</summary>
    public string Status => _status.Value ?? string.Empty;

    /// <summary>Gets a number that changes each time a change has remade the list, so a view can restore the keyboard focus the new list took away.</summary>
    [Reactive]
    public partial int EditVersion { get; private set; }

    /// <summary>Gets the command that undoes the latest edit of the document, which may be a tag edit.</summary>
    public ICommand UndoCommand => _owner.Pages.UndoCommand;

    /// <summary>Gets the choice that means a header scope or list numbering is not given.</summary>
    private static string NotSet => UiText.Get("Not set");

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Lists the tags again, keeping the same tag chosen when it is still there.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Refresh() => Refresh(SelectedItem?.Id ?? 0);

    /// <summary>Lists the tags again and chooses one.</summary>
    /// <param name="selectId">The object number of the tag to choose, or 0 for none.</param>
    public void Refresh(int selectId)
    {
        var editor = DocumentFeature<ITagEditor>.Get(_owner.TryGetDocument());
        var tags = editor is null ? [] : editor.GetTags();
        _refreshing = true;
        try
        {
            FillRoleMap(editor);
            Items = TagItemViewModel.Create(tags);
            IsTagged = tags.Count > 0;
            SelectedItem = selectId > 0 ? FindItem(selectId) : null;
        }
        finally
        {
            _refreshing = false;
        }

        ShowSelected();
    }

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Status));
        this.RaisePropertyChanged(nameof(ProblemSummary));
        this.RaisePropertyChanged(nameof(ScopeChoices));
        this.RaisePropertyChanged(nameof(ListNumberingChoices));
        this.RaisePropertyChanged(nameof(PlacementChoices));
        LocalisationRefresh.RefreshCore(Problems, visited);
        LocalisationRefresh.RefreshCore(_owner, visited);
    }

    /// <summary>Gets the choice that shows a list's numbering.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The numbering name, or the not-set choice when it has none.</returns>
    private static string NumberingName(DocumentTag list) => list.ListNumbering.Length == 0 ? NotSet : list.ListNumbering;

    /// <summary>Gets the choice that shows a tag's placement.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The placement name, or the not-set choice when it has none.</returns>
    private static string PlacementName(DocumentTag tag) => tag.Placement.Length == 0 ? NotSet : tag.Placement;

    /// <summary>Gets a text, treating a missing one as empty.</summary>
    /// <param name="value">The text, or <see langword="null"/>.</param>
    /// <returns>The text, or an empty string.</returns>
    private static string Text(string? value) => value ?? string.Empty;

    /// <summary>Adds a change when the form text differs from the tag's.</summary>
    /// <param name="changes">The changes so far.</param>
    /// <param name="field">The text.</param>
    /// <param name="current">The tag's text.</param>
    /// <param name="typed">The form's text.</param>
    private static void Add(List<TagTextChange> changes, DocumentTagField field, string? current, string typed)
    {
        var trimmed = typed.Trim();
        if (!string.Equals(Text(current), trimmed, StringComparison.Ordinal))
        {
            changes.Add(new(field, trimmed));
        }
    }

    /// <summary>Runs the accessibility check and lists what it found.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CheckAsync()
    {
        if (DocumentFeature<ITagEditor>.Get(_owner.TryGetDocument()) is not { } editor)
        {
            return;
        }

        try
        {
            var found = await Task.Run(() => editor.GetProblems(_lifetime), _lifetime).ConfigureAwait(true);
            Problems = TagProblemItem.Create(found);
            _problemSummary = found.Count switch
            {
                0 => LocalizedMessage.Get(UiText, "No problems found. A person should still read the document with a screen reader."),
                1 => LocalizedMessage.Get(UiText, "1 thing to look at."),
                _ => LocalizedMessage.Format(UiText, "{0} things to look at.", found.Count),
            };
            this.RaisePropertyChanged(nameof(ProblemSummary));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    /// <summary>Applies the form to the chosen tag: its type and any text that differs.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canChange))]
    private async Task ApplyAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var tag = item.Tag;
        var changed = 0;
        if (SelectedType is { Length: > 0 } type && !string.Equals(type, tag.Type, StringComparison.Ordinal))
        {
            if (!await RunAsync((editor, token) => editor.SetTypeAsync(tag.Id, type, token)).ConfigureAwait(true))
            {
                return;
            }

            changed++;
        }

        var others = await ApplyAttributesAsync(tag).ConfigureAwait(true);
        if (others < 0)
        {
            return;
        }

        changed += others;
        var done = changed == 0
            ? LocalizedMessage.Get(UiText, "Nothing was different, so nothing changed.")
            : LocalizedMessage
            .Format(UiText, "Saved the changes to {0}. Undo takes them back.", TagText.Message(SelectedType ?? tag.Type));
        Finish(tag.Id, done, changed > 0);
    }

    /// <summary>Applies the scope, numbering and texts in the form that differ from the tag's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>A task whose result is how many changes were made, or -1 when one was refused.</returns>
    private async Task<int> ApplyAttributesAsync(DocumentTag tag)
    {
        var changed = await ApplyCellAsync(tag).ConfigureAwait(true);
        if (changed < 0)
        {
            return -1;
        }

        if (NumberingChange(tag) is { } numbering)
        {
            if (!await RunAsync((editor, token) => editor.SetListNumberingAsync(tag.Id, numbering.Length == 0 ? null : numbering, token)).ConfigureAwait(true))
            {
                return -1;
            }

            changed++;
        }

        if (PlacementChange(tag) is { } placement)
        {
            if (!await RunAsync((editor, token) => editor.SetPlacementAsync(tag.Id, placement.Length == 0 ? null : placement, token)).ConfigureAwait(true))
            {
                return -1;
            }

            changed++;
        }

        foreach (var (field, text) in TextChanges(tag))
        {
            if (!await RunAsync((editor, token) => editor.SetTextAsync(tag.Id, field, text, token)).ConfigureAwait(true))
            {
                return -1;
            }

            changed++;
        }

        return changed;
    }

    /// <summary>Applies the header scope and the cell spans in the form that differ from the tag's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>A task whose result is how many changes were made, or -1 when one was refused.</returns>
    private async Task<int> ApplyCellAsync(DocumentTag tag)
    {
        var changed = 0;
        if (ScopeChange(tag) is { } scope)
        {
            if (!await RunAsync((editor, token) => editor.SetTableScopeAsync(tag.Id, scope, token)).ConfigureAwait(true))
            {
                return -1;
            }

            changed++;
        }

        var spans = await ApplySpansAsync(tag).ConfigureAwait(true);
        return spans < 0 ? -1 : changed + spans;
    }

    /// <summary>Applies the cell spans in the form when they differ from the tag's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>A task whose result is 1 when the spans changed, 0 when nothing differed, or -1 when they were refused.</returns>
    private async Task<int> ApplySpansAsync(DocumentTag tag)
    {
        if (!tag.IsCell)
        {
            return 0;
        }

        if (!int.TryParse(RowSpanText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var rows) || rows < 1
            || !int.TryParse(ColumnSpanText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var columns) || columns < 1)
        {
            SetStatus(LocalizedMessage.Get(UiText, "A span is a whole number of 1 or more."));
            return -1;
        }

        if (rows == tag.RowSpan && columns == tag.ColumnSpan)
        {
            return 0;
        }

        return await RunAsync((editor, token) => editor.SetCellSpansAsync(tag.Id, rows, columns, token)).ConfigureAwait(true) ? 1 : -1;
    }

    /// <summary>Adds a custom tag type and says which standard type it stands for.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task AddRoleMappingAsync()
    {
        var type = NewRoleType.Trim();
        if (type.Length == 0 || NewRoleTarget is not { Length: > 0 } target)
        {
            SetStatus(LocalizedMessage.Get(UiText, "Type a name for the custom tag and choose the standard tag it stands for."));
            return;
        }

        if (!await RunAsync((editor, token) => editor.SetRoleMappingAsync(type, target, token)).ConfigureAwait(true))
        {
            return;
        }

        NewRoleType = string.Empty;
        Finish(
            SelectedItem?.Id ?? 0,
            LocalizedMessage.Format(UiText, "Custom tag {0} now stands for {1}. Undo takes it back.", type, TagText.Message(target, true)),
            true);
    }

    /// <summary>Removes the chosen custom tag type's mapping, when no tag uses the type.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task RemoveRoleMappingAsync()
    {
        if ((uint)RoleMappingIndex >= (uint)_roleTypes.Length)
        {
            SetStatus(LocalizedMessage.Get(UiText, "Choose a custom tag type in the list first."));
            return;
        }

        var type = _roleTypes[RoleMappingIndex];
        if (!await RunAsync((editor, token) => editor.RemoveRoleMappingAsync(type, token)).ConfigureAwait(true))
        {
            return;
        }

        Finish(SelectedItem?.Id ?? 0, LocalizedMessage.Format(UiText, "Removed the mapping for custom tag {0}. Undo puts it back.", type), true);
    }

    /// <summary>Lists the custom tag types again.</summary>
    /// <param name="editor">The tag editor, or <see langword="null"/> when the document has none.</param>
    private void FillRoleMap(ITagEditor? editor)
    {
        var map = editor is null ? [] : editor.GetRoleMap();
        var lines = new string[map.Count];
        var types = new string[map.Count];
        for (var i = 0; i < lines.Length; i++)
        {
            types[i] = map[i].Type;
            lines[i] = UiText.Format("{0} stands for {1}", map[i].Type, TagText.Name(map[i].MapsTo).ToLower(CultureInfo.CurrentCulture));
        }

        _roleTypes = types;
        RoleMappings = lines;
        RoleMappingIndex = -1;
        StandardTypes = editor is null ? [] : editor.GetStandardTypes();
    }

    /// <summary>Moves the chosen tag earlier among its siblings, which changes the reading order.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveUp))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task MoveUpAsync() => MoveAsync(-1, LocalizedMessage.Get(UiText, "up"));

    /// <summary>Moves the chosen tag later among its siblings, which changes the reading order.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveDown))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task MoveDownAsync() => MoveAsync(1, LocalizedMessage.Get(UiText, "down"));

    /// <summary>Puts the chosen tag inside the tag above it, so a screen reader reads it as part of that tag.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canNest))]
    private async Task NestAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var above = TagText.Message(SiblingAboveType(IndexOf(item)), true);
        if (!await RunAsync((editor, token) => editor.NestAsync(item.Id, token)).ConfigureAwait(true))
        {
            return;
        }

        var name = TagText.Message(item.Tag.Type, true);
        Finish(
            item.Id,
            LocalizedMessage.Format(UiText, "Put the {0} tag inside the {1} tag above it. It is now one level deeper.", name, above),
            true);
    }

    /// <summary>Moves the chosen tag into the tag picked in the move list, as its last child.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canMoveInto))]
    private async Task MoveIntoAsync()
    {
        if (SelectedItem is not { } item || (uint)MoveTargetIndex >= (uint)_moveTargetIds.Length)
        {
            return;
        }

        var target = _moveTargetIds[MoveTargetIndex];
        var targetTag = FindItem(target)?.Tag;
        if (targetTag is null)
        {
            return;
        }

        var targetName = new TagRowMessage(targetTag);
        if (await RunAsync((editor, token) => editor.MoveIntoAsync(item.Id, target, token)).ConfigureAwait(true))
        {
            Finish(
                item.Id,
                LocalizedMessage.Format(UiText, "Moved the {0} tag inside {1}. Undo takes it back.", TagText.Message(item.Tag.Type, true), targetName),
                true);
        }
    }

    /// <summary>Moves the chosen tag out of its parent, to just after the parent.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canUnnest))]
    private async Task UnnestAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        if (await RunAsync((editor, token) => editor.UnnestAsync(item.Id, token)).ConfigureAwait(true))
        {
            Finish(
                item.Id,
                LocalizedMessage.Format(UiText, "Moved the {0} tag out of its parent. It is now one level higher.", TagText.Message(item.Tag.Type, true)),
                true);
        }
    }

    /// <summary>Removes the chosen tag, which holds no page content, and lifts the tags inside it. Undo puts it back.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canRemove))]
    private async Task RemoveAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        if (await RunAsync((editor, token) => editor.RemoveAsync(item.Id, token)).ConfigureAwait(true))
        {
            Finish(0, LocalizedMessage.Format(UiText, "Removed the {0} tag. Undo puts it back.", TagText.Message(item.Tag.Type, true)), true);
        }
    }

    /// <summary>Moves the chosen tag.</summary>
    /// <param name="offset">How far, and which way.</param>
    /// <param name="direction">The direction in words.</param>
    /// <returns>A task.</returns>
    private async Task MoveAsync(int offset, LocalizedMessage direction)
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        if (await RunAsync((editor, token) => editor.MoveAsync(item.Id, offset, token)).ConfigureAwait(true))
        {
            Finish(
                item.Id,
                LocalizedMessage.Format(UiText, "Moved the {0} tag {1}. Screen readers now read it in the new order.", TagText.Message(item.Tag.Type, true), direction),
                true);
        }
    }

    /// <summary>Runs one change; a refusal is written in the status.</summary>
    /// <param name="edit">The change.</param>
    /// <returns>A task whose result is <see langword="true"/> when the document changed.</returns>
    private async Task<bool> RunAsync(Func<ITagEditor, CancellationToken, ValueTask<DocumentEditResult>> edit)
    {
        if (DocumentFeature<ITagEditor>.Get(_owner.TryGetDocument()) is not { } editor)
        {
            return false;
        }

        try
        {
            var result = await edit(editor, _lifetime).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                SetStatus(result.Problem is { } problem ? LocalizedMessage.Literal(problem) : LocalizedMessage.Get(UiText, "The tags could not be changed."));
            }

            return result.Succeeded;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Lists the tags again after a change and says what happened.</summary>
    /// <param name="selectId">The tag to choose afterwards, or 0 for none.</param>
    /// <param name="done">What to say.</param>
    /// <param name="edited">Whether the document changed, which refreshes the pages.</param>
    private void Finish(int selectId, in LocalizedMessage done, bool edited)
    {
        if (edited)
        {
            Refresh(selectId);
            _owner.Pages.Refresh();
            EditVersion++;
        }

        SetStatus(done);
    }

    /// <summary>Retains a status and notifies its displayed text.</summary>
    /// <param name="message">The resource key or literal backend text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStatus(in LocalizedMessage message)
    {
        _status = message;
        this.RaisePropertyChanged(nameof(Status));
    }

    /// <summary>Lists the texts in the form that differ from the chosen tag.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>The differences.</returns>
    private TagTextChange[] TextChanges(DocumentTag tag)
    {
        var changes = new List<TagTextChange>(EditableTexts);
        Add(changes, DocumentTagField.AlternateText, tag.AlternateText, AlternateText);
        Add(changes, DocumentTagField.ActualText, tag.ActualText, ActualText);
        Add(changes, DocumentTagField.Language, tag.Language, Language);
        Add(changes, DocumentTagField.Title, tag.Title, Title);
        return [.. changes];
    }

    /// <summary>Gets the scope in the form when it differs from the header cell's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>The new scope, or <see langword="null"/> when the tag is not a header cell or nothing differs.</returns>
    private DocumentTableScope? ScopeChange(DocumentTag tag)
    {
        var index = SelectedScope is null ? -1 : ScopeChoices.IndexOf(SelectedScope);
        return tag.IsHeaderCell && index >= 0 && index != (int)tag.Scope ? (DocumentTableScope)index : null;
    }

    /// <summary>Gets the numbering in the form when it differs from the list's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>The new numbering, empty to clear it, or <see langword="null"/> when the tag is not a list or nothing differs.</returns>
    private string? NumberingChange(DocumentTag tag)
    {
        if (!tag.IsList || SelectedListNumbering is not { } chosen)
        {
            return null;
        }

        var wanted = string.Equals(chosen, NotSet, StringComparison.Ordinal) ? string.Empty : chosen;
        return string.Equals(wanted, tag.ListNumbering, StringComparison.Ordinal) ? null : wanted;
    }

    /// <summary>Gets the placement in the form when it differs from the tag's.</summary>
    /// <param name="tag">The chosen tag.</param>
    /// <returns>The new placement, empty to clear it, or <see langword="null"/> when nothing differs.</returns>
    private string? PlacementChange(DocumentTag tag)
    {
        if (!tag.CanEdit || SelectedPlacement is not { } chosen)
        {
            return null;
        }

        var wanted = string.Equals(chosen, NotSet, StringComparison.Ordinal) ? string.Empty : chosen;
        return string.Equals(wanted, tag.Placement, StringComparison.Ordinal) ? null : wanted;
    }

    /// <summary>Finds the row of a tag.</summary>
    /// <param name="id">The object number.</param>
    /// <returns>The row, or <see langword="null"/>.</returns>
    private TagItemViewModel? FindItem(int id)
    {
        foreach (var item in Items)
        {
            if (item.Id == id)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Fills the form from the chosen tag and works out what it can do.</summary>
    private void ShowSelected()
    {
        var tag = SelectedItem?.Tag;
        AlternateText = Text(tag?.AlternateText);
        ActualText = Text(tag?.ActualText);
        Language = Text(tag?.Language);
        Title = Text(tag?.Title);
        TypeChoices = ChoicesFor(tag);
        SelectedType = tag?.Type;
        ShowTableAndList(tag);
        RefreshState();
        FillMoveTargets();
    }

    /// <summary>Fills the scope, span and numbering parts of the form, which only some tags have.</summary>
    /// <param name="tag">The chosen tag, or <see langword="null"/> for none.</param>
    private void ShowTableAndList(DocumentTag? tag)
    {
        IsHeaderCell = tag is { IsHeaderCell: true };
        SelectedScope = tag is { IsHeaderCell: true } header ? ScopeChoices[(int)header.Scope] : null;
        IsCell = tag is { IsCell: true };
        RowSpanText = (tag?.RowSpan ?? 1).ToString(CultureInfo.InvariantCulture);
        ColumnSpanText = (tag?.ColumnSpan ?? 1).ToString(CultureInfo.InvariantCulture);
        IsList = tag is { IsList: true };
        SelectedListNumbering = tag is { IsList: true } list ? NumberingName(list) : null;
        CanChangePlacement = tag is { CanEdit: true };
        SelectedPlacement = tag is { CanEdit: true } editable ? PlacementName(editable) : null;
    }

    /// <summary>Lists the tags the chosen tag may move into: all but itself and the tags inside it.</summary>
    private void FillMoveTargets()
    {
        var names = new List<string>(Items.Count);
        var ids = new List<int>(Items.Count);
        var index = SelectedItem is { } selected ? IndexOf(selected) : -1;
        for (var i = 0; index >= 0 && i < Items.Count; i++)
        {
            if ((i >= index && IsWithin(index, i)) || !Items[i].Tag.CanEdit)
            {
                continue;
            }

            names.Add(Items[i].Row);
            ids.Add(Items[i].Id);
        }

        _moveTargetIds = [.. ids];
        MoveTargets = names;
        MoveTargetIndex = -1;
        CanMoveInto = false;
    }

    /// <summary>Determines whether a row is the chosen row or inside it: every row from it up to the row has a deeper depth than the chosen row.</summary>
    /// <param name="chosen">The chosen row's position.</param>
    /// <param name="candidate">The row to test.</param>
    /// <returns><see langword="true"/> when the row is the chosen row or inside it.</returns>
    private bool IsWithin(int chosen, int candidate)
    {
        var depth = Items[chosen].Tag.Depth;
        for (var i = chosen + 1; i <= candidate; i++)
        {
            if (Items[i].Tag.Depth <= depth)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the types a tag may take.</summary>
    /// <param name="tag">The tag, or <see langword="null"/> for none.</param>
    /// <returns>The types; empty when the tag cannot be changed.</returns>
    private IReadOnlyList<string> ChoicesFor(DocumentTag? tag) =>
        tag is { Id: > 0 } && DocumentFeature<ITagEditor>.Get(_owner.TryGetDocument()) is { } editor ? editor.GetTypeChoices(tag.Id) : [];

    /// <summary>Fills the form for a tag the person chose, and shows the page it is on.</summary>
    private void ChooseSelected()
    {
        if (_refreshing)
        {
            return;
        }

        ShowSelected();
        if (SelectedItem is { Tag.PageIndex: >= 0 } shown)
        {
            _owner.GoToPage(shown.Tag.PageIndex);
        }
    }

    /// <summary>Chooses the tag a problem is about and shows its page.</summary>
    private void ShowProblem()
    {
        if (SelectedProblem is not { } problem)
        {
            return;
        }

        if (problem.TagId > 0 && FindItem(problem.TagId) is { } item)
        {
            SelectedItem = item;
        }
        else if (problem.Problem.PageIndex >= 0)
        {
            _owner.GoToPage(problem.Problem.PageIndex);
        }
    }

    /// <summary>Works out what the chosen tag can do.</summary>
    private void RefreshState()
    {
        var selected = SelectedItem;
        var index = selected is null ? -1 : IndexOf(selected);
        CanChange = selected is { Tag.CanEdit: true };
        CanRemove = selected is { Tag.CanRemove: true };
        CanMoveUp = CanChange && HasSibling(index, -1);
        CanMoveDown = CanChange && HasSibling(index, 1);
        CanNest = CanMoveUp;
        CanUnnest = CanChange && selected is { Tag.Depth: > 0 };
    }

    /// <summary>Gets the type of the tag just above a row in its group.</summary>
    /// <param name="index">The row's position.</param>
    /// <returns>The raw type; empty when there is none.</returns>
    private string SiblingAboveType(int index)
    {
        if (index < 0)
        {
            return string.Empty;
        }

        var depth = Items[index].Tag.Depth;
        for (var i = index - 1; i >= 0; i--)
        {
            var other = Items[i].Tag.Depth;
            if (other == depth)
            {
                return Items[i].Tag.Type;
            }

            if (other < depth)
            {
                break;
            }
        }

        return string.Empty;
    }

    /// <summary>Finds a row's position in the list.</summary>
    /// <param name="item">The row.</param>
    /// <returns>The position, or -1.</returns>
    private int IndexOf(TagItemViewModel item)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == item.Id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Determines whether a row has a sibling in a direction: a tag at the same depth before the list leaves their shared parent.</summary>
    /// <param name="index">The row's position.</param>
    /// <param name="step">-1 to look earlier, 1 to look later.</param>
    /// <returns><see langword="true"/> when there is one.</returns>
    private bool HasSibling(int index, int step)
    {
        if (index < 0)
        {
            return false;
        }

        var depth = Items[index].Tag.Depth;
        for (var i = index + step; (uint)i < (uint)Items.Count; i += step)
        {
            var other = Items[i].Tag.Depth;
            if (other < depth)
            {
                return false;
            }

            if (other == depth)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Retains the target tag metadata while resolving its row in the current language.</summary>
    /// <param name="Tag">The target tag snapshot.</param>
    private readonly record struct TagRowMessage(DocumentTag Tag)
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => TagText.Row(Tag).Trim();
    }

    /// <summary>A text in the form that differs from the tag's.</summary>
    /// <param name="Field">The text.</param>
    /// <param name="Text">The new text; empty clears it.</param>
    private readonly record struct TagTextChange(DocumentTagField Field, string Text);
}
