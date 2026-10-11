// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json;

namespace PdfViewerLite.Core.Platform;

/// <summary>
/// Keeps the recently opened documents in a small JSON file, newest first, for desktops whose own list cannot be read
/// back reliably. Windows and macOS also tell the desktop, so documents appear in the taskbar Jump List and the Dock's
/// recent items. Missing files are skipped when the list is read.
/// </summary>
[DebuggerDisplay("JsonRecentDocumentStore: {_filePath}")]
public sealed class JsonRecentDocumentStore : IRecentDocumentStore
{
    /// <summary>The most documents remembered.</summary>
    private const int MaxRemembered = 50;

    /// <summary>The list file.</summary>
    private readonly string _filePath;

    /// <summary>Guards the file.</summary>
    private readonly Lock _gate = new();

    /// <summary>The clock.</summary>
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="JsonRecentDocumentStore"/> class.</summary>
    /// <param name="filePath">The list file.</param>
    /// <param name="time">The clock that dates each visit.</param>
    public JsonRecentDocumentStore(string filePath, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(time);
        _filePath = filePath;
        _time = time;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RecentDocument> GetRecent(int maxCount)
    {
        var entries = Read();
        var recent = new List<RecentDocument>(Math.Min(maxCount, entries.Count));
        foreach (var entry in entries)
        {
            if (recent.Count >= maxCount)
            {
                break;
            }

            if (File.Exists(entry.FilePath))
            {
                recent.Add(entry);
            }
        }

        return recent;
    }

    /// <inheritdoc/>
    public void Add(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var full = Path.GetFullPath(filePath);
        lock (_gate)
        {
            var entries = Read();
            _ = entries.RemoveAll(entry => string.Equals(entry.FilePath, full, StringComparison.Ordinal));
            entries.Insert(0, new(full, _time.GetLocalNow()));
            if (entries.Count > MaxRemembered)
            {
                entries.RemoveRange(MaxRemembered, entries.Count - MaxRemembered);
            }

            Write(entries);
        }
    }

    /// <inheritdoc/>
    public void Remove(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var full = Path.GetFullPath(filePath);
        lock (_gate)
        {
            var entries = Read();
            if (entries.RemoveAll(entry => string.Equals(entry.FilePath, full, StringComparison.Ordinal)) > 0)
            {
                Write(entries);
            }
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_gate)
        {
            Write([]);
        }
    }

    /// <summary>Reads the list, empty when it is missing or unreadable.</summary>
    /// <returns>The entries, newest first.</returns>
    private List<RecentDocument> Read()
    {
        try
        {
            using var stream = File.OpenRead(_filePath);
            return JsonSerializer.Deserialize(stream, PlatformJsonContext.Default.ListRecentDocument) ?? [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Writes the list atomically, ignoring failures.</summary>
    /// <param name="entries">The entries.</param>
    private void Write(List<RecentDocument> entries)
    {
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var temp = $"{_filePath}.tmp";
            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(stream, entries, PlatformJsonContext.Default.ListRecentDocument);
            }

            File.Move(temp, _filePath, true);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not save recent documents: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not save recent documents: {ex.Message}");
        }
    }
}
