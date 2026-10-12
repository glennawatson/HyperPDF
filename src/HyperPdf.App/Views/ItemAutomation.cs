// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Settings;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Views;

/// <summary>
/// Names list and tree items for screen readers. Without a name a list item reports its data object, which a screen
/// reader reads out as a type name, so each kind of item is described in words here. Each item also reports its place,
/// such as 3 of 40, where the desktop's accessibility interface carries it.
/// </summary>
internal static class ItemAutomation
{
    /// <summary>Names each item of a list as its container is prepared. Call it before the list is filled.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The subscription.</returns>
    internal static IDisposable NameItems(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);

        // The event carries only the container, and a container is not yet tied to its list while it is prepared,
        // so the handler captures the list to count its items. SubscribeSafe has no state-passing form.
        return list.Events().ContainerPrepared.SubscribeSafe(args => OnContainerPrepared(list, args), static error => Trace.TraceError(error.ToString()));
    }

    /// <summary>Names a list item as it is prepared for an item, from what the item holds and where it sits.</summary>
    /// <param name="list">The list.</param>
    /// <param name="args">The container that was prepared and its position.</param>
    internal static void OnContainerPrepared(ItemsControl list, ContainerPreparedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(args);
        var container = args.Container;
        var item = list.ItemFromContainer(container) ?? container.DataContext;
        var count = list.Items.Count;
        AutomationProperties.SetName(container, Describe(item, args.Index, count));
        AutomationProperties.SetPositionInSet(container, args.Index + 1);
        AutomationProperties.SetSizeOfSet(container, count);

        // Avalonia's Linux and macOS bridges drop the set position, so there it is spoken as the description.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            AutomationProperties.SetHelpText(container, DescribePosition(item, args.Index, count));
        }
    }

    /// <summary>Puts an item's place in its list into words, unless its name already says it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="index">Its zero based position.</param>
    /// <param name="count">How many items there are.</param>
    /// <returns>The description, such as "2 of 7", or <see langword="null"/>.</returns>
    internal static string? DescribePosition(object? item, int index, int count) =>
        count <= 0 || item is ThumbnailItemViewModel
            ? null
            : string.Create(CultureInfo.CurrentCulture, $"{index + 1} of {count}");

    /// <summary>Names the list or tree item that shows a view.</summary>
    /// <param name="view">The item's view.</param>
    /// <param name="name">The name.</param>
    internal static void NameContainer(Control view, string? name)
    {
        ArgumentNullException.ThrowIfNull(view);
        foreach (var ancestor in view.GetVisualAncestors())
        {
            if (ancestor is not (ListBoxItem or TreeViewItem))
            {
                continue;
            }

            AutomationProperties.SetName((Control)ancestor, name);
            return;
        }
    }

    /// <summary>Describes an item as a screen reader should say it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="index">Its zero based position.</param>
    /// <param name="count">How many items there are.</param>
    /// <returns>The description, or <see langword="null"/> for items that name themselves.</returns>
    internal static string? Describe(object? item, int index, int count) => item switch
    {
        ThumbnailItemViewModel thumbnail => DescribePage(thumbnail.Label, thumbnail.PageIndex, count),
        SearchResultItemViewModel result => string.Create(CultureInfo.CurrentCulture, $"Page {result.PageNumber}: {result.Context}"),
        AnnotationItemViewModel annotation => annotation.SpokenText,
        DocumentAttachment attachment => string.Create(CultureInfo.CurrentCulture, $"{attachment.Name}, {AttachmentsViewModel.FormatSize(attachment.Size)}"),
        LayerItemViewModel layer => layer.Name,
        RecentDocument recent => DescribeFile(recent.FileName, recent.Folder),
        DocumentTabViewModel tab => DescribeFile(tab.FileName, tab.Folder),
        FolderSearchResultViewModel found => found.SpokenText,
        PrintPreviewPage sheet => string.Create(CultureInfo.CurrentCulture, $"Sheet {sheet.Caption}"),
        PrintTarget target => target.Label,
        OutlineItemViewModel entry => entry.Title,
        RememberedCertificate certificate => DescribeCertificate(certificate),
        OptimizePresetChoice preset => $"{preset.Name}. {preset.Description}",
        ArchiveLevelChoice level => $"{level.Name}. {level.Description}",
        string text => text,
        _ => count > 0 ? string.Create(CultureInfo.CurrentCulture, $"Item {index + 1} of {count}") : null,
    };

    /// <summary>Describes a page, for example "Page 3 of 40", saying the document's own label when it differs.</summary>
    /// <param name="label">The page's label.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="count">How many pages there are.</param>
    /// <returns>The description.</returns>
    internal static string DescribePage(string label, int pageIndex, int count)
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
    internal static string DescribeTab(string fileName, bool unsaved) =>
        unsaved ? string.Create(CultureInfo.CurrentCulture, $"{fileName}, unsaved changes") : fileName;

    /// <summary>Describes a remembered certificate by who it names and where it is.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The description.</returns>
    internal static string DescribeCertificate(RememberedCertificate? certificate) =>
        certificate is null ? string.Empty : string.Create(CultureInfo.CurrentCulture, $"{certificate.Subject} ({Path.GetFileName(certificate.Path)})");

    /// <summary>Describes a file by its name and folder.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="folder">The folder.</param>
    /// <returns>The description.</returns>
    private static string DescribeFile(string fileName, string folder) =>
        folder.Length == 0 ? fileName : string.Create(CultureInfo.CurrentCulture, $"{fileName}, in {folder}");
}
