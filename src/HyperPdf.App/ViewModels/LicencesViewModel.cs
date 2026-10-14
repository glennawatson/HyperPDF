// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using HyperPdf.App.Services;
using HyperPdf.Core.Licences;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The Licences window: this application's own licence first, then every component grouped by licence. The search box
/// narrows the components by name, version, licence, origin and copyright; the chosen component's full licence text
/// can be copied.
/// </summary>
[DebuggerDisplay("LicencesViewModel: {Search}: {Nodes.Count} licences")]
public sealed partial class LicencesViewModel : ReactiveObject, IDisposable
{
    /// <summary>The text shown when no component is chosen.</summary>
    private const string NothingChosen = "Choose a component to read its licence.";

    /// <summary>The text shown when the search finds nothing.</summary>
    private const string NothingFound = "No component matches your search.";

    /// <summary>Said after the text is copied.</summary>
    private const string CopiedMessage = "Copied the licence text.";

    /// <summary>Owns the subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The notices being shown.</summary>
    private readonly NoticeDocument _document;

    /// <summary>Initializes a new instance of the <see cref="LicencesViewModel"/> class.</summary>
    /// <param name="document">The notices to show.</param>
    public LicencesViewModel(NoticeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        Nodes = Build(document, string.Empty);
        SelectedNode = FirstEntry(Nodes);
        _detailTitleHelper = this.WhenChanged(static vm => vm.SelectedNode).Select(TitleOf).ToProperty(this, static vm => vm.DetailTitle);
        _detailsHelper = this.WhenChanged(static vm => vm.SelectedNode).Select(DetailsOf).ToProperty(this, static vm => vm.Details);
        _licenceTextHelper = this.WhenChanged(static vm => vm.SelectedNode).Select(static node => node?.Entry?.Text ?? NothingChosen).ToProperty(this, static vm => vm.LicenceText);
        _subscriptions.Add(_detailTitleHelper);
        _subscriptions.Add(_detailsHelper);
        _subscriptions.Add(_licenceTextHelper);
        _subscriptions.Add(this.WhenChanged(static vm => vm.Search).SubscribeSafe(_ => Refresh(), ErrorTrace.Write));
    }

    /// <summary>Gets the interaction that puts text on the clipboard.</summary>
    public Interaction<string, RxVoid> CopyInteraction { get; } = new();

    /// <summary>Gets or sets the words the components are narrowed by.</summary>
    [Reactive]
    public partial string Search { get; set; } = string.Empty;

    /// <summary>Gets the licence headings with the components below them that match the search.</summary>
    [Reactive(nameof(Summary))]
    public partial IReadOnlyList<LicenceNode> Nodes { get; private set; } = [];

    /// <summary>Gets or sets the chosen row.</summary>
    [Reactive]
    public partial LicenceNode? SelectedNode { get; set; }

    /// <summary>Gets what was last done, for screen readers, such as "Copied the licence text."</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Gets the heading of the chosen row.</summary>
    [ObservableAsProperty]
    public partial string DetailTitle { get; }

    /// <summary>Gets the licence, version, origin, copyright and link of the chosen component.</summary>
    [ObservableAsProperty]
    public partial string Details { get; }

    /// <summary>Gets the full licence text of the chosen component.</summary>
    [ObservableAsProperty]
    public partial string LicenceText { get; }

    /// <summary>Gets a short count of what the search shows, such as "12 components".</summary>
    public string Summary => CountText(Nodes);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Builds the tree of rows for a search.</summary>
    /// <param name="document">The notices.</param>
    /// <param name="search">The words to match; empty matches everything.</param>
    /// <returns>The licence rows that have at least one matching component.</returns>
    internal static List<LicenceNode> Build(NoticeDocument document, string search)
    {
        var words = search.Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nodes = new List<LicenceNode>();
        foreach (var group in document.Groups)
        {
            var children = new List<LicenceNode>();
            foreach (var entry in group.Entries)
            {
                if (Matches(entry, words))
                {
                    children.Add(new(Describe(entry), entry, []));
                }
            }

            if (children.Count > 0)
            {
                nodes.Add(new(string.Create(CultureInfo.CurrentCulture, $"{group.Licence} ({children.Count})"), null, children));
            }
        }

        return nodes;
    }

    /// <summary>Asks the window to close.</summary>
    [ReactiveCommand]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Close()
    {
    }

    /// <summary>Counts the components under the licence rows in words.</summary>
    /// <param name="nodes">The licence rows.</param>
    /// <returns>For example "12 components", or a note that nothing matches.</returns>
    private static string CountText(IReadOnlyList<LicenceNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count += node.Children.Count;
        }

        if (count == 0)
        {
            return NothingFound;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{count} {(count == 1 ? "component" : "components")}");
    }

    /// <summary>Names a component with its version, such as "Avalonia 12.0.0".</summary>
    /// <param name="entry">The component.</param>
    /// <returns>The name.</returns>
    private static string Describe(NoticeEntry entry) => entry.Version.Length == 0 ? entry.Name : $"{entry.Name} {entry.Version}";

    /// <summary>Determines whether a component contains every search word in its name, version, licence, origin or copyright.</summary>
    /// <param name="entry">The component.</param>
    /// <param name="words">The search words.</param>
    /// <returns><see langword="true"/> when every word is found.</returns>
    private static bool Matches(NoticeEntry entry, string[] words)
    {
        foreach (var word in words)
        {
            var found = entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Version.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Licence.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Origin.Contains(word, StringComparison.OrdinalIgnoreCase)
                || entry.Copyright.Contains(word, StringComparison.OrdinalIgnoreCase);
            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the first component row.</summary>
    /// <param name="nodes">The licence rows.</param>
    /// <returns>The row, or <see langword="null"/> when there are none.</returns>
    private static LicenceNode? FirstEntry(IReadOnlyList<LicenceNode> nodes) => nodes.Count > 0 && nodes[0].Children.Count > 0 ? nodes[0].Children[0] : null;

    /// <summary>Finds the row of a component, so a chosen component stays chosen when the search changes.</summary>
    /// <param name="nodes">The licence rows.</param>
    /// <param name="entry">The component.</param>
    /// <returns>The row, or <see langword="null"/> when the search hides it.</returns>
    private static LicenceNode? Find(IReadOnlyList<LicenceNode> nodes, NoticeEntry entry)
    {
        foreach (var group in nodes)
        {
            foreach (var child in group.Children)
            {
                if (ReferenceEquals(child.Entry, entry))
                {
                    return child;
                }
            }
        }

        return null;
    }

    /// <summary>Gets the heading for the chosen row.</summary>
    /// <param name="node">The row.</param>
    /// <returns>The heading.</returns>
    private static string TitleOf(LicenceNode? node) => node?.Entry?.Name ?? node?.Title ?? string.Empty;

    /// <summary>Lists the facts about the chosen row, one per line.</summary>
    /// <param name="node">The row.</param>
    /// <returns>The lines.</returns>
    private static string DetailsOf(LicenceNode? node)
    {
        if (node?.Entry is not { } entry)
        {
            return node is null ? string.Empty : string.Create(CultureInfo.CurrentCulture, $"{node.Children.Count} components use this licence.");
        }

        var text = new StringBuilder();
        _ = text.Append("Licence: ").Append(entry.Licence);
        Append(text, "Version", entry.Version);
        Append(text, "Comes from", entry.Origin);
        Append(text, "Copyright", entry.Copyright);
        Append(text, "Link", entry.Link);
        return text.ToString();
    }

    /// <summary>Adds a labelled line when it has a value.</summary>
    /// <param name="text">The text so far.</param>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    private static void Append(StringBuilder text, string label, string value)
    {
        if (value.Length > 0)
        {
            _ = text.Append('\n').Append(label).Append(": ").Append(value);
        }
    }

    /// <summary>Puts the chosen licence text on the clipboard.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CopyAsync()
    {
        if (SelectedNode?.Entry is not { } entry)
        {
            return;
        }

        _ = await CopyInteraction.Handle(entry.Text).ToTask().ConfigureAwait(true);
        Status = CopiedMessage;
    }

    /// <summary>Rebuilds the rows for the search and keeps the chosen component when it still matches.</summary>
    private void Refresh()
    {
        var previous = SelectedNode?.Entry;
        Nodes = Build(_document, Search);
        SelectedNode = (previous is null ? null : Find(Nodes, previous)) ?? FirstEntry(Nodes);
    }
}
