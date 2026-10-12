// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using HyperPdf.Core.Annotations;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The sidebar's comment list: every annotation in page order, and the ones shown after the filters and sort order
/// are applied. Filtering never moves the page or the pick; it only changes what the list shows.
/// </summary>
public sealed partial class AnnotationsViewModel
{
    /// <summary>The author filter choice that shows everyone's comments.</summary>
    private const string EveryoneName = "Everyone";

    /// <summary>The type filter that shows every type.</summary>
    private const int AllTypes = 0;

    /// <summary>The type filter for highlights and other text marks.</summary>
    private const int TextMarksType = 1;

    /// <summary>The type filter for notes.</summary>
    private const int NotesType = 2;

    /// <summary>The type filter for drawings, lines and shapes.</summary>
    private const int DrawingsType = 3;

    /// <summary>The type filter for text boxes and callouts.</summary>
    private const int TextType = 4;

    /// <summary>The type filter for stamps and signatures.</summary>
    private const int StampsType = 5;

    /// <summary>The sort order by page, the order <see cref="Items"/> is kept in.</summary>
    private const int SortByPage = 0;

    /// <summary>The sort order by date, newest first.</summary>
    private const int SortByDate = 1;

    /// <summary>The sort order by author.</summary>
    private const int SortByAuthor = 2;

    /// <summary>The sort order by type.</summary>
    private const int SortByType = 3;

    /// <summary>The type filter each kind belongs to, by the kind's value.</summary>
    private static readonly int[] KindTypes =
    [
        AllTypes,
        TextMarksType,
        TextMarksType,
        TextMarksType,
        TextMarksType,
        DrawingsType,
        NotesType,
        TextType,
        StampsType,
        DrawingsType,
        DrawingsType,
        DrawingsType,
        DrawingsType,
        StampsType,
        TextType,
        DrawingsType,
        DrawingsType,
        DrawingsType,
    ];

    /// <summary>Gets the author filter choice that shows everyone's comments.</summary>
    public static string Everyone => EveryoneName;

    /// <summary>Gets the type filter choices; the index is <see cref="FilterType"/>.</summary>
    public static IReadOnlyList<string> TypeFilterOptions { get; } = ["All types", "Highlights and text marks", "Notes", "Drawings and shapes", "Text and callouts", "Stamps and signatures"];

    /// <summary>Gets the colour filter choices; the index is <see cref="FilterColor"/>, and 0 shows every colour.</summary>
    public static IReadOnlyList<string> ColorFilterOptions { get; } = CreateColorFilterOptions();

    /// <summary>Gets the sort order choices; the index is <see cref="SortOrder"/>.</summary>
    public static IReadOnlyList<string> SortOptions { get; } = ["Page", "Newest first", "Author", "Type"];

    /// <summary>Gets every annotation of the document, in page order.</summary>
    public ObservableCollection<AnnotationItemViewModel> Items { get; } = [];

    /// <summary>Gets the annotations the list shows: <see cref="Items"/> filtered and sorted.</summary>
    public ObservableCollection<AnnotationItemViewModel> VisibleItems { get; } = [];

    /// <summary>Gets the author filter choices: everyone, then each author in the document.</summary>
    public ObservableCollection<string> AuthorOptions { get; } = [EveryoneName];

    /// <summary>Gets or sets the text the list is filtered by: words in the note, replies, author or type.</summary>
    [Reactive]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>Gets or sets the type filter, an index into <see cref="TypeFilterOptions"/>.</summary>
    [Reactive]
    public partial int FilterType { get; set; }

    /// <summary>Gets or sets the colour filter, an index into <see cref="ColorFilterOptions"/>.</summary>
    [Reactive]
    public partial int FilterColor { get; set; }

    /// <summary>Gets or sets the author filter, one of <see cref="AuthorOptions"/>.</summary>
    [Reactive]
    public partial string FilterAuthor { get; set; } = EveryoneName;

    /// <summary>Gets or sets the sort order, an index into <see cref="SortOptions"/>.</summary>
    [Reactive]
    public partial int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the list's filters and sort order are shown.</summary>
    [Reactive]
    public partial bool ShowFilters { get; set; }

    /// <summary>Gets a value indicating whether there are annotations but the filters hide all of them.</summary>
    [Reactive]
    public partial bool NoMatches { get; private set; }

    /// <summary>Determines whether an annotation kind belongs to a type filter.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="type">The filter, an index into <see cref="TypeFilterOptions"/>.</param>
    /// <returns><see langword="true"/> when it belongs.</returns>
    public static bool IsOfType(AnnotationKind kind, int type) => type == AllTypes || ((uint)kind < (uint)KindTypes.Length && KindTypes[(int)kind] == type);

    /// <summary>Rebuilds the sidebar list from every page; called when the annotations panel is shown.</summary>
    public void RefreshItems()
    {
        Items.Clear();
        if (Editor is not { } editor)
        {
            ApplyFilter();
            return;
        }

        for (var page = 0; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            editor.GetAnnotations(page, _scratch);
            foreach (var annotation in _scratch)
            {
                Items.Add(CreateItem(editor, annotation, _owner.GetPageDisplay(page)));
            }
        }

        ApplyFilter();
    }

    /// <summary>Lists the colour filter choices: all colours, then each named colour.</summary>
    /// <returns>The choices.</returns>
    private static string[] CreateColorFilterOptions()
    {
        var options = new string[AnnotationColors.All.Count + 1];
        options[0] = "All colours";
        for (var i = 0; i < AnnotationColors.All.Count; i++)
        {
            options[i + 1] = AnnotationColors.All[i].Name;
        }

        return options;
    }

    /// <summary>Makes a sidebar item for an annotation, with its replies.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="label">The page label.</param>
    /// <returns>The item.</returns>
    private static AnnotationItemViewModel CreateItem(IAnnotationEditor editor, PageAnnotation annotation, string label)
    {
        var replies = new List<AnnotationReply>();
        editor.GetReplies(annotation.PageIndex, annotation.Index, replies);
        return new(annotation, label, replies);
    }

    /// <summary>Compares two items in a sort order, falling back to page order so equal items keep their places.</summary>
    /// <param name="order">The sort order.</param>
    /// <param name="left">The first item.</param>
    /// <param name="right">The second item.</param>
    /// <returns>The comparison.</returns>
    private static int Compare(int order, AnnotationItemViewModel left, AnnotationItemViewModel right)
    {
        var first = order switch
        {
            SortByDate => Nullable.Compare(right.Annotation.Modified, left.Annotation.Modified),
            SortByAuthor => string.Compare(left.AuthorName, right.AuthorName, StringComparison.CurrentCultureIgnoreCase),
            SortByType => string.Compare(left.KindName, right.KindName, StringComparison.CurrentCulture),
            _ => 0,
        };
        if (first != 0)
        {
            return first;
        }

        var page = left.Annotation.PageIndex.CompareTo(right.Annotation.PageIndex);
        return page != 0 ? page : left.Annotation.Index.CompareTo(right.Annotation.Index);
    }

    /// <summary>Determines whether an item passes the filters.</summary>
    /// <param name="item">The item.</param>
    /// <param name="text">The trimmed filter text, read once per pass.</param>
    /// <returns><see langword="true"/> when it is shown.</returns>
    private bool Matches(AnnotationItemViewModel item, string text)
    {
        if (!IsOfType(item.Annotation.Kind, FilterType))
        {
            return false;
        }

        if (FilterColor > 0 && FilterColor < ColorFilterOptions.Count && !string.Equals(item.ColorName, ColorFilterOptions[FilterColor], StringComparison.Ordinal))
        {
            return false;
        }

        if (FilterAuthor is { Length: > 0 } author && !string.Equals(author, EveryoneName, StringComparison.Ordinal) && !string.Equals(item.AuthorName, author, StringComparison.Ordinal))
        {
            return false;
        }

        return text.Length == 0 || item.SearchText.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Determines whether every filter is at its show-everything choice.</summary>
    /// <param name="text">The trimmed filter text.</param>
    /// <returns><see langword="true"/> when nothing is filtered out.</returns>
    private bool ShowsEverything(string text) =>
        text.Length == 0 && FilterType == AllTypes && FilterColor == 0 && (FilterAuthor is not { Length: > 0 } author || string.Equals(author, EveryoneName, StringComparison.Ordinal));

    /// <summary>Shows the items that pass the filters, in the chosen order, and offers each author found.</summary>
    private void ApplyFilter()
    {
        if (Items.Count == 0)
        {
            // Nothing to filter: clear what is left without building anything.
            VisibleItems.Clear();
            UpdateAuthors([]);
            NoMatches = false;
            return;
        }

        var authors = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var text = FilterText.Trim();
        var everything = ShowsEverything(text);
        var shown = new List<AnnotationItemViewModel>(Items.Count);
        foreach (var item in Items)
        {
            _ = authors.Add(item.AuthorName);
            if (everything || Matches(item, text))
            {
                shown.Add(item);
            }
        }

        // Items are already in page order, so page order needs no sort.
        var order = SortOrder;
        if (order != SortByPage)
        {
            shown.Sort((left, right) => Compare(order, left, right));
        }

        if (!SameItems(shown))
        {
            VisibleItems.Clear();
            foreach (var item in shown)
            {
                VisibleItems.Add(item);
            }
        }

        UpdateAuthors(authors);
        NoMatches = VisibleItems.Count == 0;
    }

    /// <summary>Determines whether the list already shows exactly these items in this order, so it need not be rebuilt.</summary>
    /// <param name="shown">The items to show.</param>
    /// <returns><see langword="true"/> when nothing would change.</returns>
    private bool SameItems(List<AnnotationItemViewModel> shown)
    {
        if (shown.Count != VisibleItems.Count)
        {
            return false;
        }

        for (var i = 0; i < shown.Count; i++)
        {
            if (!ReferenceEquals(shown[i], VisibleItems[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Updates the author choices in place, keeping the chosen author when it is still there.</summary>
    /// <param name="authors">The authors found, in order.</param>
    private void UpdateAuthors(SortedSet<string> authors)
    {
        var index = 1;
        foreach (var author in authors)
        {
            if (index < AuthorOptions.Count && string.Equals(AuthorOptions[index], author, StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            AuthorOptions.Insert(index, author);
            index++;
        }

        while (AuthorOptions.Count > index)
        {
            var removed = AuthorOptions[^1];
            AuthorOptions.RemoveAt(AuthorOptions.Count - 1);
            if (string.Equals(removed, FilterAuthor, StringComparison.Ordinal))
            {
                FilterAuthor = EveryoneName;
            }
        }
    }

    /// <summary>Replaces one page's entries in the list, keeping page order.</summary>
    /// <param name="page">The page.</param>
    private void RefreshPage(int page)
    {
        var insertAt = 0;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var itemPage = Items[i].Annotation.PageIndex;
            if (itemPage == page)
            {
                Items.RemoveAt(i);
                insertAt = i;
            }
            else if (itemPage < page && insertAt == 0)
            {
                insertAt = i + 1;
            }
        }

        if (Editor is { } editor)
        {
            _scratch.Clear();
            editor.GetAnnotations(page, _scratch);
            var label = _owner.GetPageDisplay(page);
            foreach (var annotation in _scratch)
            {
                Items.Insert(Math.Min(insertAt, Items.Count), CreateItem(editor, annotation, label));
                insertAt++;
            }
        }

        ApplyFilter();
    }

    /// <summary>Shows every annotation again.</summary>
    [ReactiveCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        FilterType = 0;
        FilterColor = 0;
        FilterAuthor = EveryoneName;
    }
}
