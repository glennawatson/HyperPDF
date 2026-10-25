// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Plain words for the colour separation pages of a preseparated document.</summary>
internal static class SeparationText
{
    /// <summary>Gets the name shown for a separation whose file gives no colourant.</summary>
    internal static string UnnamedColorant => UiText.Get("Unnamed colourant");

    /// <summary>Gets says that the current page of a preseparated document is an ordinary page.</summary>
    internal static string OrdinaryPage => UiText.Get("Ordinary page, not a colour separation");

    /// <summary>Gets a colourant's display name.</summary>
    /// <param name="page">The separation page.</param>
    /// <returns>The colourant, or <see cref="UnnamedColorant"/>.</returns>
    internal static string Colorant(SeparationPage page) => page.Colorant.Length > 0 ? page.Colorant : UnnamedColorant;

    /// <summary>Describes a separation page, for example "Separation: Cyan, part of printed page 1 (4 colourants)".</summary>
    /// <param name="group">The printed page.</param>
    /// <param name="page">The separation page.</param>
    /// <returns>The description.</returns>
    internal static string Describe(SeparationGroup group, SeparationPage page)
    {
        var count = group.Pages.Length;
        return UiText.Format(
            "Separation: {0}, part of printed page {1} ({2} {3})",
            Colorant(page),
            group.PrintedPage,
            count,
            (count == 1 ? UiText.Get("colourant") : UiText.Get("colourants")));
    }

    /// <summary>Says what a page edit could not keep together, or <see langword="null"/> when it kept everything.</summary>
    /// <param name="changes">The printed pages that changed.</param>
    /// <returns>The notice.</returns>
    internal static string? ChangeNotice(IReadOnlyList<SeparationChange> changes)
    {
        if (changes.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var change in changes)
        {
            _ = text.Append(text.Length == 0 ? string.Empty : " ").Append(Describe(change));
        }

        return text.ToString();
    }

    /// <summary>Describes one change.</summary>
    /// <param name="change">The change.</param>
    /// <returns>The sentence or sentences.</returns>
    private static string Describe(SeparationChange change)
    {
        if (change.PrintedPage == 0)
        {
            return UiText.Format("Damaged colour separation details on {0} were removed.", Pages(change.UnlinkedPages));
        }

        var kept = change.KeptPages < change.GroupPages
            ? UiText.Format(
            "Printed page {0} kept {1} of its {2} colour separations, and they stay linked.",
            change.PrintedPage,
            change.KeptPages,
            change.GroupPages)
            : string.Empty;
        var marked = change.UnlinkedPages == 1 ? UiText.Get("is no longer marked as a colour separation") : UiText.Get("are no longer marked as colour separations");
        var unlinked = change.UnlinkedPages > 0
            ? UiText.Format(
            "{0} of printed page {1} {2}.",
            Pages(change.UnlinkedPages),
            change.PrintedPage,
            marked)
            : string.Empty;
        return kept.Length > 0 && unlinked.Length > 0 ? $"{kept} {unlinked}" : kept + unlinked;
    }

    /// <summary>Counts pages in words.</summary>
    /// <param name="count">The count.</param>
    /// <returns>For example "1 page" or "2 pages".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Pages(int count) => $"{count} {(count == 1 ? UiText.Get("page") : UiText.Get("pages"))}";
}
