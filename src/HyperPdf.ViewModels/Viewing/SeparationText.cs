// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Plain words for the colour separation pages of a preseparated document.</summary>
internal static class SeparationText
{
    /// <summary>The name shown for a separation whose file gives no colourant.</summary>
    internal const string UnnamedColorant = "Unnamed colourant";

    /// <summary>Says that the current page of a preseparated document is an ordinary page.</summary>
    internal const string OrdinaryPage = "Ordinary page, not a colour separation";

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
        return string.Create(
            CultureInfo.CurrentCulture,
            $"Separation: {Colorant(page)}, part of printed page {group.PrintedPage} ({count} {(count == 1 ? "colourant" : "colourants")})");
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
            return string.Create(CultureInfo.CurrentCulture, $"Damaged colour separation details on {Pages(change.UnlinkedPages)} were removed.");
        }

        var kept = change.KeptPages < change.GroupPages
            ? string.Create(CultureInfo.CurrentCulture, $"Printed page {change.PrintedPage} kept {change.KeptPages} of its {change.GroupPages} colour separations, and they stay linked.")
            : string.Empty;
        var marked = change.UnlinkedPages == 1 ? "is no longer marked as a colour separation" : "are no longer marked as colour separations";
        var unlinked = change.UnlinkedPages > 0
            ? string.Create(CultureInfo.CurrentCulture, $"{Pages(change.UnlinkedPages)} of printed page {change.PrintedPage} {marked}.")
            : string.Empty;
        return kept.Length > 0 && unlinked.Length > 0 ? $"{kept} {unlinked}" : kept + unlinked;
    }

    /// <summary>Counts pages in words.</summary>
    /// <param name="count">The count.</param>
    /// <returns>For example "1 page" or "2 pages".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Pages(int count) => string.Create(CultureInfo.CurrentCulture, $"{count} {(count == 1 ? "page" : "pages")}");
}
