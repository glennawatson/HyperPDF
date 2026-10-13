// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Core.Licences;

/// <summary>Reads the third-party notices file line by line, collecting each licence text in a pooled buffer.</summary>
internal ref struct NoticeReader
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

    /// <summary>The characters the text buffer holds when it is first rented.</summary>
    private const int InitialTextLength = 4096;

    /// <summary>The factor by which the text buffer grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The finished groups; made with the first one.</summary>
    private List<NoticeGroup>? _groups;

    /// <summary>The entries of the current group; made with the first one.</summary>
    private List<NoticeEntry>? _entries;

    /// <summary>The licence text being read, in a rented array.</summary>
    private char[]? _text;

    /// <summary>The characters of the licence text read so far.</summary>
    private int _textLength;

    /// <summary>The current group's licence.</summary>
    private string? _licence;

    /// <summary>The current component's name, or <see langword="null"/> before the first component.</summary>
    private string? _name;

    /// <summary>The current component's version field.</summary>
    private string? _version;

    /// <summary>The current component's origin field.</summary>
    private string? _origin;

    /// <summary>The current component's copyright field.</summary>
    private string? _copyright;

    /// <summary>The current component's link field.</summary>
    private string? _link;

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
            _textLength = 0;
        }
        else if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
        {
            CloseGroup();
            _licence = line[GroupPrefix.Length..].Trim().ToString();
        }
        else if (line.StartsWith(EntryPrefix, StringComparison.Ordinal))
        {
            _name = line[EntryPrefix.Length..].Trim().ToString();
            ClearFields();
        }
        else if (line.StartsWith(FieldPrefix, StringComparison.Ordinal))
        {
            AddField(line[FieldPrefix.Length..]);
        }
    }

    /// <summary>Finishes the last group and gives the text buffer back.</summary>
    /// <returns>The groups.</returns>
    internal List<NoticeGroup> Finish()
    {
        CloseGroup();
        if (_text is not null)
        {
            ArrayPool<char>.Shared.Return(_text);
            _text = null;
        }

        return _groups ?? [];
    }

    /// <summary>Handles a line of licence text; the closing fence ends the component.</summary>
    /// <param name="line">The line.</param>
    private void AddTextLine(ReadOnlySpan<char> line)
    {
        if (!line.SequenceEqual(FenceClose))
        {
            AppendText(line);
            return;
        }

        _inText = false;
        if (_name is not null)
        {
            (_entries ??= []).Add(new(
                _name,
                _licence ?? string.Empty,
                _version ?? string.Empty,
                _origin ?? string.Empty,
                _copyright ?? string.Empty,
                _link ?? string.Empty,
                _text.AsSpan(0, _textLength).TrimEnd().ToString()));
        }

        _name = null;
        ClearFields();
    }

    /// <summary>Adds a line and a line feed to the licence text.</summary>
    /// <param name="line">The line.</param>
    private void AppendText(ReadOnlySpan<char> line)
    {
        var needed = _textLength + line.Length + 1;
        if (_text is null || needed > _text.Length)
        {
            var larger = ArrayPool<char>.Shared.Rent(Math.Max(Math.Max(InitialTextLength, needed), (_text?.Length ?? 0) * GrowthFactor));
            _text.AsSpan(0, _textLength).CopyTo(larger);
            if (_text is not null)
            {
                ArrayPool<char>.Shared.Return(_text);
            }

            _text = larger;
        }

        line.CopyTo(_text.AsSpan(_textLength));
        _text[_textLength + line.Length] = '\n';
        _textLength = needed;
    }

    /// <summary>Records a <c>Name: value</c> line; a name that is not one of the fields is ignored.</summary>
    /// <param name="field">The line without its prefix.</param>
    private void AddField(ReadOnlySpan<char> field)
    {
        var split = field.IndexOf(FieldSeparator, StringComparison.Ordinal);
        if (split <= 0)
        {
            return;
        }

        var name = field[..split];
        var value = field[(split + FieldSeparator.Length)..].Trim();
        if (name.SequenceEqual(nameof(NoticeEntry.Version)))
        {
            _version = value.ToString();
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Origin)))
        {
            _origin = value.ToString();
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Copyright)))
        {
            _copyright = value.ToString();
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Link)))
        {
            _link = value.ToString();
        }
    }

    /// <summary>Forgets the current component's fields.</summary>
    private void ClearFields()
    {
        _version = null;
        _origin = null;
        _copyright = null;
        _link = null;
    }

    /// <summary>Adds the current group to the result when it has components.</summary>
    private void CloseGroup()
    {
        if (_entries is { Count: > 0 })
        {
            (_groups ??= []).Add(new(_licence ?? string.Empty, _entries));
        }

        _entries = null;
    }
}
