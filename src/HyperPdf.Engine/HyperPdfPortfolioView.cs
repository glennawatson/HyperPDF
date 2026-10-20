// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Core.Attachments;
using HyperPdfLibrary;
using HyperPdfLibrary.Portfolio;

namespace HyperPdf.Engine;

/// <summary>
/// Turns a PDF portfolio's collection data into what the attachments panel shows: the files in the order the portfolio's sort
/// keys ask for, each with the folder it sits in and the field values the portfolio's schema makes visible. Damaged
/// collection data never stops the files being listed: a file the collection does not describe is listed after those it does.
/// </summary>
internal static class HyperPdfPortfolioView
{
    /// <summary>The key of the file name in a portfolio's sort keys and schema.</summary>
    private const string FileKey = "F";

    /// <summary>The schema subtype of a number field.</summary>
    private const string NumberSubtype = "N";

    /// <summary>The separator between the folder names of a path.</summary>
    private const char FolderSeparator = '/';

    /// <summary>Adds the portfolio's order, details and initial file to the attachments it lists.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <param name="attachments">The attachments, in index order; entries the portfolio describes are replaced.</param>
    internal static void Apply(PdfPortfolio portfolio, DocumentAttachment[] attachments)
    {
        var items = new List<PdfPortfolioItem>(portfolio.Items.Count);
        foreach (var item in portfolio.Items)
        {
            if ((uint)item.AttachmentIndex < (uint)attachments.Length)
            {
                items.Add(item);
            }
        }

        items.Sort((first, second) => Compare(first, second, portfolio));
        var folders = FolderPaths(portfolio.RootFolder);
        var columns = VisibleColumns(portfolio);
        for (var rank = 0; rank < items.Count; rank++)
        {
            var item = items[rank];
            var initial = portfolio.InitialDocument is { } name && string.Equals(name, item.FileName, StringComparison.OrdinalIgnoreCase);
            attachments[item.AttachmentIndex] = attachments[item.AttachmentIndex] with
            {
                PortfolioOrder = rank,
                PortfolioDetails = Details(item, folders, columns),
                IsPortfolioInitial = initial,
            };
        }
    }

    /// <summary>Summarises how a portfolio asks to be shown.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <returns>The summary.</returns>
    internal static PortfolioSummary Summarise(PdfPortfolio portfolio) => new(portfolio.View, portfolio.Items.Count, portfolio.InitialDocument);

    /// <summary>Compares two files by the portfolio's sort keys, then by name.</summary>
    /// <param name="first">The first file.</param>
    /// <param name="second">The second file.</param>
    /// <param name="portfolio">The portfolio.</param>
    /// <returns>A negative number when the first goes first, positive when the second does, else zero.</returns>
    private static int Compare(PdfPortfolioItem first, PdfPortfolioItem second, PdfPortfolio portfolio)
    {
        foreach (var sort in portfolio.Sort)
        {
            var result = CompareValues(ValueOf(first, sort.Key), ValueOf(second, sort.Key), IsNumber(portfolio, sort.Key));
            if (result != 0)
            {
                return sort.Ascending ? result : -result;
            }
        }

        return string.Compare(first.FileName, second.FileName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Compares two field values: numbers as numbers, anything else as text, and a missing value after any present one.</summary>
    /// <param name="first">The first value, or <see langword="null"/>.</param>
    /// <param name="second">The second value, or <see langword="null"/>.</param>
    /// <param name="numbers">Whether the field holds numbers.</param>
    /// <returns>The comparison.</returns>
    private static int CompareValues(string? first, string? second, bool numbers)
    {
        if (first is null || second is null)
        {
            return (first is null ? 1 : 0) - (second is null ? 1 : 0);
        }

        return numbers && TryParseNumber(first, out var one) && TryParseNumber(second, out var other)
            ? one.CompareTo(other)
            : string.Compare(first, second, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads a number from a field value.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the number.</param>
    /// <returns><see langword="true"/> when the text is a number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>Gets a file's value for a sort or schema key.</summary>
    /// <param name="item">The file.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or <see langword="null"/> when the file has none.</returns>
    private static string? ValueOf(PdfPortfolioItem item, string key) =>
        key == FileKey ? item.FileName : item.Fields.GetValueOrDefault(key);

    /// <summary>Determines whether the schema says a field holds numbers.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <param name="key">The field's key.</param>
    /// <returns><see langword="true"/> for a number field.</returns>
    private static bool IsNumber(PdfPortfolio portfolio, string key)
    {
        foreach (var field in portfolio.Schema)
        {
            if (field.Key == key)
            {
                return field.Subtype == NumberSubtype;
            }
        }

        return false;
    }

    /// <summary>Lists the schema fields the portfolio shows, in its order.</summary>
    /// <param name="portfolio">The portfolio.</param>
    /// <returns>The visible fields.</returns>
    private static List<PdfPortfolioField> VisibleColumns(PdfPortfolio portfolio)
    {
        var columns = new List<PdfPortfolioField>(portfolio.Schema.Count);
        foreach (var field in portfolio.Schema)
        {
            if (field.Visible && field.Key != FileKey)
            {
                columns.Add(field);
            }
        }

        columns.Sort(static (first, second) => first.Order.CompareTo(second.Order));
        return columns;
    }

    /// <summary>Writes a file's folder and field values as one line.</summary>
    /// <param name="item">The file.</param>
    /// <param name="folders">The path of each folder, by folder id.</param>
    /// <param name="columns">The fields to show.</param>
    /// <returns>The line, or <see langword="null"/> when there is nothing to show.</returns>
    private static string? Details(PdfPortfolioItem item, Dictionary<int, string> folders, List<PdfPortfolioField> columns)
    {
        var builder = new StringBuilder();
        if (item.FolderId is { } id && folders.TryGetValue(id, out var path) && path.Length > 0)
        {
            _ = builder.Append("Folder: ").Append(path);
        }

        foreach (var column in columns)
        {
            if (item.Fields.GetValueOrDefault(column.Key) is not { Length: > 0 } value)
            {
                continue;
            }

            _ = builder.Append(builder.Length > 0 ? "; " : string.Empty).Append(column.Name.Length > 0 ? column.Name : column.Key).Append(": ").Append(value);
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    /// <summary>Works out the path of every folder from the root.</summary>
    /// <param name="root">The root folder, or <see langword="null"/>.</param>
    /// <returns>The path of each folder, by folder id; the root's path is empty.</returns>
    private static Dictionary<int, string> FolderPaths(PdfPortfolioFolder? root)
    {
        var paths = new Dictionary<int, string>();
        if (root is not null)
        {
            Walk(root, string.Empty, paths, 0);
        }

        return paths;
    }

    /// <summary>Records a folder's path and the paths of the folders below it.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="parent">The path of its parent.</param>
    /// <param name="paths">Receives the paths.</param>
    /// <param name="depth">How far below the root the folder is.</param>
    private static void Walk(PdfPortfolioFolder folder, string parent, Dictionary<int, string> paths, int depth)
    {
        var path = parent;
        if (!string.IsNullOrEmpty(folder.Name))
        {
            path = parent.Length == 0 ? folder.Name : $"{parent}{FolderSeparator}{folder.Name}";
        }

        paths[folder.Id] = path;
        if (depth >= PdfLimits.MaxNesting)
        {
            return;
        }

        foreach (var child in folder.Children)
        {
            Walk(child, path, paths, depth + 1);
        }
    }
}
