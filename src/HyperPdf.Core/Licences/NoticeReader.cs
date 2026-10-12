// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Licences;

/// <summary>Reads the third-party notices file line by line.</summary>
[DebuggerDisplay("NoticeReader: {_licence}")]
internal sealed class NoticeReader
{
    /// <summary>The line that opens a licence text.</summary>
    private const string FenceOpen = "````text";

    /// <summary>The line that closes a licence text.</summary>
    private const string FenceClose = "````";

    /// <summary>The prefix of a group heading.</summary>
    private const string GroupPrefix = "## ";

    /// <summary>The prefix of a component heading.</summary>
    private const string EntryPrefix = "### ";

    /// <summary>The prefix of a field line.</summary>
    private const string FieldPrefix = "- ";

    /// <summary>The text between a field's name and its value.</summary>
    private const string FieldSeparator = ": ";

    /// <summary>The finished groups.</summary>
    private readonly List<NoticeGroup> _groups = [];

    /// <summary>The licence text being read.</summary>
    private readonly StringBuilder _text = new();

    /// <summary>The entries of the current group.</summary>
    private List<NoticeEntry> _entries = [];

    /// <summary>The current group's licence.</summary>
    private string _licence = string.Empty;

    /// <summary>The current component's name, or <see langword="null"/> before the first component.</summary>
    private string? _name;

    /// <summary>The current component's fields by name.</summary>
    private Dictionary<string, string> _fields = [with(StringComparer.Ordinal)];

    /// <summary>Whether the reader is inside a licence text.</summary>
    private bool _inText;

    /// <summary>Handles one line.</summary>
    /// <param name="line">The line.</param>
    internal void Add(ReadOnlySpan<char> line)
    {
        if (_inText)
        {
            AddTextLine(line);
        }
        else if (line.SequenceEqual(FenceOpen))
        {
            _inText = true;
            _ = _text.Clear();
        }
        else if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
        {
            CloseGroup();
            _licence = line[GroupPrefix.Length..].Trim().ToString();
        }
        else if (line.StartsWith(EntryPrefix, StringComparison.Ordinal))
        {
            _name = line[EntryPrefix.Length..].Trim().ToString();
            _fields = [with(StringComparer.Ordinal)];
        }
        else if (line.StartsWith(FieldPrefix, StringComparison.Ordinal))
        {
            AddField(line[FieldPrefix.Length..]);
        }
    }

    /// <summary>Finishes the last group.</summary>
    /// <returns>The groups.</returns>
    internal List<NoticeGroup> Finish()
    {
        CloseGroup();
        return _groups;
    }

    /// <summary>Handles a line of licence text; the closing fence ends the component.</summary>
    /// <param name="line">The line.</param>
    private void AddTextLine(ReadOnlySpan<char> line)
    {
        if (!line.SequenceEqual(FenceClose))
        {
            _ = _text.Append(line).Append('\n');
            return;
        }

        _inText = false;
        if (_name is not null)
        {
            _entries.Add(new(
                _name,
                _licence,
                Field(nameof(NoticeEntry.Version)),
                Field(nameof(NoticeEntry.Origin)),
                Field(nameof(NoticeEntry.Copyright)),
                Field(nameof(NoticeEntry.Link)),
                _text.ToString().TrimEnd()));
        }

        _name = null;
        _fields = [with(StringComparer.Ordinal)];
    }

    /// <summary>Records a <c>Name: value</c> line.</summary>
    /// <param name="field">The line without its prefix.</param>
    private void AddField(ReadOnlySpan<char> field)
    {
        var split = field.IndexOf(FieldSeparator, StringComparison.Ordinal);
        if (split > 0)
        {
            _fields[field[..split].ToString()] = field[(split + FieldSeparator.Length)..].Trim().ToString();
        }
    }

    /// <summary>Gets a field of the current component.</summary>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string Field(string name) => _fields.GetValueOrDefault(name, string.Empty);

    /// <summary>Adds the current group to the result when it has components.</summary>
    private void CloseGroup()
    {
        if (_entries.Count > 0)
        {
            _groups.Add(new(_licence, _entries));
        }

        _entries = [];
    }
}
