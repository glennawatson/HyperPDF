// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.Platform.Linux.Recent;

/// <summary>
/// Reads and writes the freedesktop.org <c>recently-used.xbel</c> file. KDE (Dolphin's Recent Files, the application
/// launcher) and GTK applications share it, so documents opened here show up across the desktop.
/// </summary>
[DebuggerDisplay("XbelRecentDocumentStore: {FilePath}")]
public sealed class XbelRecentDocumentStore : IRecentDocumentStore
{
    /// <summary>The PDF MIME type.</summary>
    private const string PdfMimeType = "application/pdf";

    /// <summary>The application name recorded in bookmarks.</summary>
    private const string ApplicationName = "PdfViewerLite";

    /// <summary>The maximum number of bookmarks kept in the file.</summary>
    private const int MaxBookmarks = 500;

    /// <summary>The XBEL timestamp format.</summary>
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

    /// <summary>The bookmark extension namespace.</summary>
    private static readonly XNamespace BookmarkNs = "http://www.freedesktop.org/standards/desktop-bookmarks";

    /// <summary>The shared MIME info namespace.</summary>
    private static readonly XNamespace MimeNs = "http://www.freedesktop.org/standards/shared-mime-info";

    /// <summary>The <c>bookmark</c> element.</summary>
    private static readonly XName BookmarkElement = "bookmark";

    /// <summary>The <c>info</c> element.</summary>
    private static readonly XName InfoElement = "info";

    /// <summary>The <c>metadata</c> element.</summary>
    private static readonly XName MetadataElement = "metadata";

    /// <summary>The <c>bookmark:applications</c> element.</summary>
    private static readonly XName ApplicationsElement = BookmarkNs + "applications";

    /// <summary>The <c>bookmark:application</c> element.</summary>
    private static readonly XName ApplicationElement = BookmarkNs + "application";

    /// <summary>The <c>mime:mime-type</c> element.</summary>
    private static readonly XName MimeTypeElement = MimeNs + "mime-type";

    /// <summary>The <c>href</c> attribute.</summary>
    private static readonly XName HrefAttribute = "href";

    /// <summary>The <c>added</c> attribute.</summary>
    private static readonly XName AddedAttribute = "added";

    /// <summary>The <c>modified</c> attribute.</summary>
    private static readonly XName ModifiedAttribute = "modified";

    /// <summary>The <c>visited</c> attribute.</summary>
    private static readonly XName VisitedAttribute = "visited";

    /// <summary>The <c>count</c> attribute.</summary>
    private static readonly XName CountAttribute = "count";

    /// <summary>The <c>name</c> attribute.</summary>
    private static readonly XName NameAttribute = "name";

    /// <summary>Supplies the current time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="XbelRecentDocumentStore"/> class at the default location.</summary>
    public XbelRecentDocumentStore()
        : this(Path.Combine(XdgDirectories.DataHome, "recently-used.xbel"), TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="XbelRecentDocumentStore"/> class.</summary>
    /// <param name="filePath">The XBEL file.</param>
    /// <param name="timeProvider">Supplies the current time.</param>
    public XbelRecentDocumentStore(string filePath, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(timeProvider);
        FilePath = filePath;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the XBEL file path.</summary>
    public string FilePath { get; }

    /// <inheritdoc/>
    public IReadOnlyList<RecentDocument> GetRecent(int maxCount)
    {
        var root = Load()?.Root;
        if (root is null)
        {
            return [];
        }

        var results = new List<RecentDocument>();
        foreach (var bookmark in root.Elements(BookmarkElement))
        {
            if (TryCreateRecent(bookmark, out var recent))
            {
                results.Add(recent);
            }
        }

        results.Sort(static (a, b) => b.Visited.CompareTo(a.Visited));
        if (results.Count > maxCount)
        {
            results.RemoveRange(maxCount, results.Count - maxCount);
        }

        return results;
    }

    /// <inheritdoc/>
    public void Add(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var uri = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        var now = _timeProvider.GetUtcNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var document = Load() ?? CreateDocument();
        var root = document.Root!;

        var bookmark = FindBookmark(root, uri);
        if (bookmark is null)
        {
            bookmark = new(
                BookmarkElement,
                new XAttribute(HrefAttribute, uri),
                new XAttribute(AddedAttribute, now),
                new XElement(
                    InfoElement,
                    new XElement(
                        MetadataElement,
                        new XAttribute("owner", "http://freedesktop.org"),
                        new XElement(MimeTypeElement, new XAttribute("type", PdfMimeType)),
                        new XElement(ApplicationsElement))));
            root.Add(bookmark);
        }

        bookmark.SetAttributeValue(ModifiedAttribute, now);
        bookmark.SetAttributeValue(VisitedAttribute, now);
        UpdateApplication(bookmark, now);
        Trim(root);
        Save(document);
    }

    /// <inheritdoc/>
    public void Remove(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var document = Load();
        var root = document?.Root;
        if (root is null)
        {
            return;
        }

        var uri = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        var bookmark = FindBookmark(root, uri);
        if (bookmark is null || !IsPdf(bookmark))
        {
            return;
        }

        bookmark.Remove();
        Save(document!);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        var document = Load();
        var root = document?.Root;
        if (root is null)
        {
            return;
        }

        var changed = false;
        var bookmarks = new List<XElement>(root.Elements(BookmarkElement));
        foreach (var bookmark in bookmarks)
        {
            if (!IsPdf(bookmark))
            {
                continue;
            }

            bookmark.Remove();
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        Save(document!);
    }

    /// <summary>Creates a recent document entry from a bookmark when it is an existing local PDF.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="recent">The entry.</param>
    /// <returns><see langword="true"/> when the bookmark describes an existing local PDF.</returns>
    private static bool TryCreateRecent(XElement bookmark, out RecentDocument recent)
    {
        recent = null!;
        if (!IsPdf(bookmark) || !TryGetLocalPath(bookmark, out var path) || !File.Exists(path))
        {
            return false;
        }

        var visited = (string?)bookmark.Attribute(VisitedAttribute) ?? (string?)bookmark.Attribute(ModifiedAttribute);
        recent = new(path, ParseDate(visited));
        return true;
    }

    /// <summary>Finds the bookmark for a URI.</summary>
    /// <param name="root">The root element.</param>
    /// <param name="uri">The URI.</param>
    /// <returns>The bookmark, if present.</returns>
    private static XElement? FindBookmark(XElement root, string uri)
    {
        foreach (var candidate in root.Elements(BookmarkElement))
        {
            if (string.Equals((string?)candidate.Attribute(HrefAttribute), uri, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Records this application in a bookmark, incrementing its use count.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="now">The timestamp.</param>
    private static void UpdateApplication(XElement bookmark, string now)
    {
        var metadata = bookmark.Element(InfoElement)?.Element(MetadataElement);
        if (metadata is null)
        {
            return;
        }

        var applications = metadata.Element(ApplicationsElement);
        if (applications is null)
        {
            applications = new(ApplicationsElement);
            metadata.Add(applications);
        }

        var application = FindApplication(applications);
        if (application is null)
        {
            application = new(ApplicationElement, new XAttribute(NameAttribute, ApplicationName), new XAttribute("exec", "'pdfviewerlite %u'"), new XAttribute(CountAttribute, "0"));
            applications.Add(application);
        }

        _ = int.TryParse((string?)application.Attribute(CountAttribute), NumberStyles.None, CultureInfo.InvariantCulture, out var count);
        application.SetAttributeValue(CountAttribute, (count + 1).ToString(CultureInfo.InvariantCulture));
        application.SetAttributeValue(ModifiedAttribute, now);
    }

    /// <summary>Finds this application's entry.</summary>
    /// <param name="applications">The applications element.</param>
    /// <returns>The entry, if present.</returns>
    private static XElement? FindApplication(XElement applications)
    {
        foreach (var candidate in applications.Elements(ApplicationElement))
        {
            if (string.Equals((string?)candidate.Attribute(NameAttribute), ApplicationName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Determines whether a bookmark describes a PDF.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <returns><see langword="true"/> for PDFs.</returns>
    private static bool IsPdf(XElement bookmark)
    {
        var mime = (string?)bookmark.Element(InfoElement)?.Element(MetadataElement)?.Element(MimeTypeElement)?.Attribute("type");
        if (string.Equals(mime, PdfMimeType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var uri = (string?)bookmark.Attribute(HrefAttribute);
        return uri?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Gets the local path of a <c>file://</c> bookmark.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> for local files.</returns>
    private static bool TryGetLocalPath(XElement bookmark, out string path)
    {
        path = string.Empty;
        var uriText = (string?)bookmark.Attribute(HrefAttribute);
        if (uriText is null || !Uri.TryCreate(uriText, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return false;
        }

        path = uri.LocalPath;
        return true;
    }

    /// <summary>Parses an XBEL timestamp.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The timestamp, or the minimum value.</returns>
    private static DateTimeOffset ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.MinValue;

    /// <summary>Removes the oldest bookmarks beyond the limit.</summary>
    /// <param name="root">The root element.</param>
    private static void Trim(XElement root)
    {
        var bookmarks = new List<XElement>(root.Elements(BookmarkElement));
        if (bookmarks.Count <= MaxBookmarks)
        {
            return;
        }

        bookmarks.Sort(static (a, b) => ParseDate((string?)a.Attribute(ModifiedAttribute)).CompareTo(ParseDate((string?)b.Attribute(ModifiedAttribute))));
        for (var i = 0; i < bookmarks.Count - MaxBookmarks; i++)
        {
            bookmarks[i].Remove();
        }
    }

    /// <summary>Creates an empty XBEL document.</summary>
    /// <returns>The document.</returns>
    private static XDocument CreateDocument() => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XElement(
            "xbel",
            new XAttribute("version", "1.0"),
            new XAttribute(XNamespace.Xmlns + "bookmark", BookmarkNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "mime", MimeNs.NamespaceName)));

    /// <summary>Loads the file without resolving external entities.</summary>
    /// <returns>The document, or <see langword="null"/> when missing or invalid.</returns>
    private XDocument? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(FilePath, settings);
            var document = XDocument.Load(reader);
            return document.Root?.Name.LocalName == "xbel" ? document : null;
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes the file atomically.</summary>
    /// <param name="document">The document.</param>
    private void Save(XDocument document)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            var temp = $"{FilePath}.{Environment.ProcessId}.tmp";
            using (var writer = XmlWriter.Create(temp, new() { Indent = true, IndentChars = "  " }))
            {
                document.Save(writer);
            }

            File.Move(temp, FilePath, true);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not update {FilePath}: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not update {FilePath}: {ex.Message}");
        }
    }
}
