// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Licences;

/// <summary>
/// The third-party notices file read into groups of components. The file is Markdown written by
/// <c>src/tools/GenerateThirdPartyNotices.cs</c>: a <c>## </c> heading per licence, a <c>### </c> heading per component
/// with <c>- Name: value</c> lines, then the licence text in a four-backtick <c>text</c> fence. Reading the file only
/// notes where each part sits; the groups and their strings are made the first time <see cref="Groups"/> is read.
/// </summary>
[DebuggerDisplay("NoticeDocument: {_rows.Length} components")]
public sealed class NoticeDocument
{
    /// <summary>The notices text; the rows point into it.</summary>
    private readonly string _source;

    /// <summary>Where each component's parts sit in the text.</summary>
    private readonly NoticeRow[] _rows;

    /// <summary>The groups once made, or <see langword="null"/>.</summary>
    private IReadOnlyList<NoticeGroup>? _groups;

    /// <summary>Initializes a new instance of the <see cref="NoticeDocument"/> class.</summary>
    /// <param name="source">The notices text.</param>
    /// <param name="rows">Where each component's parts sit in the text.</param>
    private NoticeDocument(string source, NoticeRow[] rows)
    {
        _source = source;
        _rows = rows;
    }

    /// <summary>Gets an empty document.</summary>
    public static NoticeDocument Empty { get; } = new(string.Empty, []);

    /// <summary>Gets the groups, this application's licence first.</summary>
    public IReadOnlyList<NoticeGroup> Groups => Volatile.Read(ref _groups) ?? ReadGroups();

    /// <summary>Reads a notices file.</summary>
    /// <param name="utf8">The file's bytes.</param>
    /// <returns>The document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NoticeDocument Parse(ReadOnlySpan<byte> utf8) => Parse(Encoding.UTF8.GetString(utf8));

    /// <summary>Reads a notices file; the document keeps the text, and does not copy it.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The document.</returns>
    public static NoticeDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var reader = new NoticeReader(text);
        var start = 0;
        while (start < text.Length)
        {
            var feed = text.AsSpan(start).IndexOfAny('\r', '\n');
            var end = feed < 0 ? text.Length : start + feed;
            var next = feed < 0 ? text.Length : end + 1;
            if (feed >= 0 && text[end] == '\r' && next < text.Length && text[next] == '\n')
            {
                next++;
            }

            reader.Add(start, end - start, next);
            start = next;
        }

        var rows = reader.Finish();
        return rows.Length == 0 ? Empty : new(text, rows);
    }

    /// <summary>Gets every component of every group.</summary>
    /// <returns>The components.</returns>
    public List<NoticeEntry> AllEntries()
    {
        var all = new List<NoticeEntry>(_rows.Length);
        var groups = Groups;
        for (var i = 0; i < groups.Count; i++)
        {
            all.AddRange(groups[i].Entries);
        }

        return all;
    }

    /// <summary>Makes the groups and keeps them.</summary>
    /// <returns>The groups.</returns>
    private IReadOnlyList<NoticeGroup> ReadGroups()
    {
        if (_rows.Length == 0)
        {
            return [];
        }

        var groups = new NoticeGroup[GroupCount()];
        var group = 0;
        for (var start = 0; start < _rows.Length;)
        {
            var end = GroupEnd(start);
            var entries = new NoticeEntry[end - start];
            var licence = _rows[start].Licence.Read(_source);
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = new(_source, in _rows[start + i], licence);
            }

            groups[group] = new(licence, entries);
            group++;
            start = end;
        }

        return Interlocked.CompareExchange(ref _groups, groups, null) ?? groups;
    }

    /// <summary>Counts the licence headings that have components.</summary>
    /// <returns>The group count.</returns>
    private int GroupCount()
    {
        var count = 0;
        for (var start = 0; start < _rows.Length; start = GroupEnd(start))
        {
            count++;
        }

        return count;
    }

    /// <summary>Finds the first component after a licence group.</summary>
    /// <param name="start">The group's first component.</param>
    /// <returns>The index after the group.</returns>
    private int GroupEnd(int start)
    {
        var end = start + 1;
        while (end < _rows.Length && _rows[end].Licence.Start == _rows[start].Licence.Start)
        {
            end++;
        }

        return end;
    }
}
