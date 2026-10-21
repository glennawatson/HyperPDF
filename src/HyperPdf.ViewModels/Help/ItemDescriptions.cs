// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;

namespace HyperPdf.ViewModels.Help;

/// <summary>
/// Says each kind of list or tree item in words for a screen reader. Without a name a list item reports its data object,
/// which a screen reader reads out as a type name, so every render head names its items from here.
/// </summary>
public static class ItemDescriptions
{
    /// <summary>The name of a reader with no document open.</summary>
    private const string NoDocument = "No document";

    /// <summary>Puts an item's place in its list into words, unless its name already says it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="index">Its zero based position.</param>
    /// <param name="count">How many items there are.</param>
    /// <returns>The description, such as "2 of 7", or <see langword="null"/>.</returns>
    public static string? DescribePosition(object? item, int index, int count) =>
        count <= 0 || item is ThumbnailItemViewModel
            ? null
            : string.Create(CultureInfo.CurrentCulture, $"{index + 1} of {count}");

    /// <summary>Describes an item as a screen reader should say it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="index">Its zero based position.</param>
    /// <param name="count">How many items there are.</param>
    /// <returns>The description, or <see langword="null"/> for items that name themselves.</returns>
    public static string? Describe(object? item, int index, int count) => item switch
    {
        ThumbnailItemViewModel { Colorant: { } colorant } separation => $"{DescribePage(separation.Label, separation.PageIndex, count)}, {colorant} separation",
        ThumbnailItemViewModel thumbnail => DescribePage(thumbnail.Label, thumbnail.PageIndex, count),
        SearchResultItemViewModel result => string.Create(CultureInfo.CurrentCulture, $"Page {result.PageNumber}: {result.Context}"),
        AnnotationItemViewModel annotation => annotation.SpokenText,
        DocumentAttachment attachment => string.Create(CultureInfo.CurrentCulture, $"{attachment.Name}, {AttachmentsViewModel.FormatSize(attachment.Size)}"),
        LayerItemViewModel layer => layer.SpokenName,
        PartItemViewModel part => part.SpokenName,
        ThreadItemViewModel thread => thread.SpokenName,
        TagItemViewModel tag => tag.SpokenName,
        TagProblemItem problem => problem.Text,
        RecentDocument recent => DescribeFile(recent.FileName, recent.Folder),
        DocumentTabViewModel tab => DescribeFile(tab.FileName, tab.Folder),
        FolderSearchResultViewModel found => found.SpokenText,
        PrintPreviewPage sheet => string.Create(CultureInfo.CurrentCulture, $"Sheet {sheet.Caption}"),
        PrintTarget target => target.Label,
        OutlineItemViewModel entry => entry.Title,
        RememberedCertificate certificate => DescribeCertificate(certificate),
        OptimizePresetChoice preset => $"{preset.Name}. {preset.Description}",
        ArchiveLevelChoice level => $"{level.Name}. {level.Description}",
        ProtectionFormatChoice format => $"{format.Name}. {format.Description}",
        FlattenScopeChoice scope => $"{scope.Name}. {scope.Description}",
        FlattenLookChoice look => $"{look.Name}. {look.Description}",
        ValidationActionChoice action => $"{action.Name}. {action.Description}",
        SigningKeyChoice key => key.Name,
        ExternalResultChoice result => result.Name,
        string text => text,
        _ => count > 0 ? string.Create(CultureInfo.CurrentCulture, $"Item {index + 1} of {count}") : null,
    };

    /// <summary>Describes a page, for example "Page 3 of 40", saying the document's own label when it differs.</summary>
    /// <param name="label">The page's label.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="count">How many pages there are.</param>
    /// <returns>The description.</returns>
    public static string DescribePage(string label, int pageIndex, int count)
    {
        var number = (pageIndex + 1).ToString(CultureInfo.CurrentCulture);
        return label == number
            ? string.Create(CultureInfo.CurrentCulture, $"Page {number} of {count}")
            : string.Create(CultureInfo.CurrentCulture, $"Page {label}, {number} of {count}");
    }

    /// <summary>Describes an open tab in the tab strip: its file, and whether it has changes that are not saved.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="unsaved">Whether there are unsaved changes.</param>
    /// <returns>The description.</returns>
    public static string DescribeTab(string fileName, bool unsaved) =>
        unsaved ? string.Create(CultureInfo.CurrentCulture, $"{fileName}, unsaved changes") : fileName;

    /// <summary>Describes a remembered certificate by who it names and where it is.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The description.</returns>
    public static string DescribeCertificate(RememberedCertificate? certificate) =>
        certificate is null ? string.Empty : string.Create(CultureInfo.CurrentCulture, $"{certificate.Subject} ({Path.GetFileName(certificate.Path)})");

    /// <summary>Describes where the reader is, for example "Page 5 of 40", using the document's own page labels.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The description.</returns>
    public static string DescribeReader(DocumentTabViewModel? tab)
    {
        if (tab is not { PageCount: > 0 } shown)
        {
            return NoDocument;
        }

        var index = Math.Clamp(shown.CurrentPageIndex, 0, shown.PageCount - 1);
        return DescribePage(shown.GetPageDisplay(index), index, shown.PageCount);
    }

    /// <summary>Describes the picked annotation, for example "Selected: Rectangle, yellow, on page 2 (Delete removes it, arrow keys move it)".</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The description, or an empty string when nothing is picked.</returns>
    public static string DescribeSelection(DocumentTabViewModel? tab)
    {
        if (tab?.Annotations.Selected is not { } picked)
        {
            return string.Empty;
        }

        var action = picked.IsMovable ? "Delete removes it, arrow keys move it" : "Delete removes it";
        return $"Selected: {AnnotationNames.Get(picked.Kind)}, {AnnotationNames.GetColor(picked.Color)}, on page {picked.PageIndex + 1} ({action})";
    }

    /// <summary>Describes a file by its name and folder.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="folder">The folder.</param>
    /// <returns>The description.</returns>
    private static string DescribeFile(string fileName, string folder) =>
        folder.Length == 0 ? fileName : string.Create(CultureInfo.CurrentCulture, $"{fileName}, in {folder}");
}
