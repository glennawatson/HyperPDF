// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>An entry of the document parts tree: a part with its name, metadata and the pages it covers.</summary>
[DebuggerDisplay("PartItemViewModel: {Name} {PagesText}")]
public sealed class PartItemViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>Initializes a new instance of the <see cref="PartItemViewModel"/> class.</summary>
    /// <param name="part">The part.</param>
    /// <param name="children">The entries listed under this one.</param>
    public PartItemViewModel(DocumentPart part, IReadOnlyList<PartItemViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(children);
        Name = part.Name;
        Details = part.Details;
        PagesText = DescribePages(part.StartPage, part.EndPage);
        Children = children;
        TargetPage = part.StartPage ?? FirstTarget(children);
        SpokenName = Speak(part, PagesText);
    }

    /// <summary>Gets the part's name.</summary>
    public string Name { get; }

    /// <summary>Gets the part's metadata as "key: value" pairs, or an empty string.</summary>
    public string Details { get; }

    /// <summary>Gets a value indicating whether the part has metadata to show.</summary>
    public bool HasDetails => Details.Length > 0;

    /// <summary>Gets the pages the part covers in words, such as "Pages 2 to 3", or an empty string for a part that only groups others.</summary>
    public string PagesText { get; }

    /// <summary>Gets the parts listed under this one.</summary>
    public IReadOnlyList<PartItemViewModel> Children { get; }

    /// <summary>Gets the zero based page choosing the part goes to: its first page, or the first page of the first part inside it; <see langword="null"/> when there is none.</summary>
    public int? TargetPage { get; }

    /// <summary>Gets how a screen reader says the entry.</summary>
    public string SpokenName { get; }

    /// <summary>Puts a page range into words.</summary>
    /// <param name="start">The zero based first page, or <see langword="null"/>.</param>
    /// <param name="end">The zero based last page, or <see langword="null"/>.</param>
    /// <returns>The words, such as "Page 4" or "Pages 2 to 3"; empty for none.</returns>
    public static string DescribePages(int? start, int? end)
    {
        if (start is not { } first)
        {
            return string.Empty;
        }

        var last = Math.Max(first, end ?? first);
        return first == last ? UiText.Format("Page {0}", first + 1) : UiText.Format("Pages {0} to {1}", first + 1, last + 1);
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => LocalisationRefresh.RefreshCore(Children, visited);

    /// <summary>Finds the page the first part that has one starts on.</summary>
    /// <param name="children">The parts.</param>
    /// <returns>The page, or <see langword="null"/>.</returns>
    private static int? FirstTarget(IReadOnlyList<PartItemViewModel> children)
    {
        foreach (var child in children)
        {
            if (child.TargetPage is { } page)
            {
                return page;
            }
        }

        return null;
    }

    /// <summary>Says an entry as a screen reader should.</summary>
    /// <param name="part">The part.</param>
    /// <param name="pages">The pages in words.</param>
    /// <returns>The words.</returns>
    private static string Speak(DocumentPart part, string pages)
    {
        var spoken = pages.Length == 0 ? part.Name : $"{part.Name}, {pages}";
        return part.Details.Length == 0 ? spoken : $"{spoken}. {part.Details}";
    }
}
