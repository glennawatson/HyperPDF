// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>The document properties dialog.</summary>
[DebuggerDisplay("PropertiesViewModel: {Title}")]
public sealed partial class PropertiesViewModel : ReactiveObject
{
    /// <summary>Bytes in a kibibyte.</summary>
    private const double Kibibyte = 1024;

    /// <summary>Millimetres per PDF point.</summary>
    private const double MillimetresPerPoint = 25.4 / 72;

    /// <summary>Initializes a new instance of the <see cref="PropertiesViewModel"/> class.</summary>
    /// <param name="tab">The tab.</param>
    public PropertiesViewModel(DocumentTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Title = tab.Title;
        var metadata = tab.Source.Metadata;
        var info = new FileInfo(tab.FilePath);
        var entries = new List<PropertyEntry>
        {
            new("Location", tab.FilePath),
            new("File size", info.Exists ? FormatSize(info.Length) : string.Empty),
            new("Pages", tab.PageCount.ToString(CultureInfo.CurrentCulture)),
        };
        Add(entries, "Colour separations", tab.Separations.Summary);
        if (metadata is not null)
        {
            AddMetadata(entries, metadata);
        }

        if (tab.Source.PageSizes is [var first, ..])
        {
            entries.Add(new("Page size", string.Create(CultureInfo.CurrentCulture, $"{first.Width * MillimetresPerPoint:0} × {first.Height * MillimetresPerPoint:0} mm")));
        }

        // Every property of the XMP packet is listed, so none needs the file's bytes to be read.
        foreach (var property in DocumentFeature<IDocumentXmpSource>.Get(tab.TryGetDocument())?.GetProperties() ?? [])
        {
            Add(entries, $"XMP {property.Name}", property.Value);
        }

        Entries = entries;
    }

    /// <summary>Gets the dialog title.</summary>
    public string Title { get; }

    /// <summary>Gets the properties.</summary>
    public IReadOnlyList<PropertyEntry> Entries { get; }

    /// <summary>Asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Close()
    {
    }

    /// <summary>Adds the rows the document's information gives.</summary>
    /// <param name="entries">The list.</param>
    /// <param name="metadata">The document's metadata.</param>
    private static void AddMetadata(List<PropertyEntry> entries, DocumentMetadata metadata)
    {
        Add(entries, nameof(Title), metadata.Title);
        Add(entries, "Author", metadata.Author);
        Add(entries, "Subject", metadata.Subject);
        Add(entries, "Keywords", metadata.Keywords);
        Add(entries, "Creator", metadata.Creator);
        Add(entries, "Producer", metadata.Producer);
        Add(entries, "Created", metadata.Created?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        Add(entries, "Modified", metadata.Modified?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        Add(entries, "PDF version", metadata.FormatVersion);
        entries.Add(new(metadata.WrappedFileName is null ? "Encrypted" : "Wrapper encrypted", metadata.IsEncrypted ? "Yes" : "No"));
        Add(entries, "Integrity protection (PDF MAC)", metadata.Integrity?.Summary);
        AddFileFacts(entries, metadata);
        Add(entries, "Encrypted file inside", metadata.WrappedFileName);
        Add(entries, "Cryptographic filter", metadata.WrappedCryptographicFilter);
    }

    /// <summary>Adds the rows that say how the file is built: whether it is linearized and whether it is a tagged PDF.</summary>
    /// <param name="entries">The list.</param>
    /// <param name="metadata">The document's metadata.</param>
    private static void AddFileFacts(List<PropertyEntry> entries, DocumentMetadata metadata)
    {
        entries.Add(new("Linearized", metadata.IsLinearized ? "Yes" : "No"));
        entries.Add(new("Tagged PDF", metadata.OpenHints.IsTagged ? "Yes" : "No"));
    }

    /// <summary>Adds a property when it has a value.</summary>
    /// <param name="entries">The list.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    private static void Add(List<PropertyEntry> entries, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new(name, value));
        }
    }

    /// <summary>Formats a byte count.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    private static string FormatSize(long bytes)
    {
        var size = (double)bytes;
        string[] units = ["bytes", "KiB", "MiB", "GiB"];
        var unit = 0;
        while (size >= Kibibyte && unit < units.Length - 1)
        {
            size /= Kibibyte;
            unit++;
        }

        return string.Create(CultureInfo.CurrentCulture, $"{size:0.#} {units[unit]}");
    }
}
