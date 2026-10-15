// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Core.Licences;

/// <summary>Reads the third-party notices file line by line, noting where each component's parts are without copying any text.</summary>
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

    /// <summary>The rows the row buffer holds when it is first rented.</summary>
    private const int InitialRows = 128;

    /// <summary>The factor by which the row buffer grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The file's characters.</summary>
    private readonly ReadOnlySpan<char> _source;

    /// <summary>The finished rows, in a rented array.</summary>
    private NoticeRow[]? _rows;

    /// <summary>The rows finished so far.</summary>
    private int _count;

    /// <summary>The current group's licence.</summary>
    private TextRange _licence;

    /// <summary>The current component's name.</summary>
    private TextRange _name;

    /// <summary>The current component's version field.</summary>
    private TextRange _version;

    /// <summary>The current component's origin field.</summary>
    private TextRange _origin;

    /// <summary>The current component's copyright field.</summary>
    private TextRange _copyright;

    /// <summary>The current component's link field.</summary>
    private TextRange _link;

    /// <summary>The index of the first character of the licence text being read.</summary>
    private int _textStart;

    /// <summary>Whether a component heading has been read since the last licence text.</summary>
    private bool _hasName;

    /// <summary>Whether the reader is inside a licence text.</summary>
    private bool _inText;

    /// <summary>Initializes a new instance of the <see cref="NoticeReader"/> struct.</summary>
    /// <param name="source">The file's characters; the ranges the reader records point into it.</param>
    internal NoticeReader(ReadOnlySpan<char> source) => _source = source;

    /// <summary>Handles one line.</summary>
    /// <param name="start">The index of the line's first character.</param>
    /// <param name="length">The line's length without its line break.</param>
    /// <param name="next">The index of the first character of the next line.</param>
    internal void Add(int start, int length, int next)
    {
        var line = _source.Slice(start, length);
        if (_inText)
        {
            AddTextLine(line, start);
        }
        else if (line.SequenceEqual(FenceOpen))
        {
            _inText = true;
            _textStart = next;
        }
        else if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
        {
            _licence = Trimmed(start + GroupPrefix.Length, length - GroupPrefix.Length);
        }
        else if (line.StartsWith(EntryPrefix, StringComparison.Ordinal))
        {
            _name = Trimmed(start + EntryPrefix.Length, length - EntryPrefix.Length);
            _hasName = true;
            ClearFields();
        }
        else if (line.StartsWith(FieldPrefix, StringComparison.Ordinal))
        {
            AddField(start + FieldPrefix.Length, length - FieldPrefix.Length);
        }
    }

    /// <summary>Gives the row buffer back and returns the rows.</summary>
    /// <returns>The rows, in file order.</returns>
    internal NoticeRow[] Finish()
    {
        if (_rows is null)
        {
            return [];
        }

        var rows = _rows.AsSpan(0, _count).ToArray();
        ArrayPool<NoticeRow>.Shared.Return(_rows);
        _rows = null;
        return rows;
    }

    /// <summary>Handles a line of licence text; the closing fence ends the component.</summary>
    /// <param name="line">The line.</param>
    /// <param name="start">The index of the line's first character.</param>
    private void AddTextLine(ReadOnlySpan<char> line, int start)
    {
        if (!line.SequenceEqual(FenceClose))
        {
            return;
        }

        _inText = false;
        if (_hasName)
        {
            AddRow(new(_licence, _name, _version, _origin, _copyright, _link, new(_textStart, start - _textStart)));
        }

        _hasName = false;
        ClearFields();
    }

    /// <summary>Adds a finished row, growing the buffer when it is full.</summary>
    /// <param name="row">The row.</param>
    private void AddRow(in NoticeRow row)
    {
        if (_rows is null)
        {
            _rows = ArrayPool<NoticeRow>.Shared.Rent(InitialRows);
        }
        else if (_count == _rows.Length)
        {
            var larger = ArrayPool<NoticeRow>.Shared.Rent(_rows.Length * GrowthFactor);
            _rows.CopyTo(larger, 0);
            ArrayPool<NoticeRow>.Shared.Return(_rows);
            _rows = larger;
        }

        _rows[_count] = row;
        _count++;
    }

    /// <summary>Records a <c>Name: value</c> line; a name that is not one of the fields is ignored.</summary>
    /// <param name="start">The index of the field's first character, after its prefix.</param>
    /// <param name="length">The length of the field.</param>
    private void AddField(int start, int length)
    {
        var field = _source.Slice(start, length);
        var split = field.IndexOf(FieldSeparator, StringComparison.Ordinal);
        if (split <= 0)
        {
            return;
        }

        var name = field[..split];
        var value = Trimmed(start + split + FieldSeparator.Length, length - split - FieldSeparator.Length);
        if (name.SequenceEqual(nameof(NoticeEntry.Version)))
        {
            _version = value;
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Origin)))
        {
            _origin = value;
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Copyright)))
        {
            _copyright = value;
        }
        else if (name.SequenceEqual(nameof(NoticeEntry.Link)))
        {
            _link = value;
        }
    }

    /// <summary>Gets the range left after white space is trimmed from both ends of some characters.</summary>
    /// <param name="start">The index of the first character.</param>
    /// <param name="length">The number of characters.</param>
    /// <returns>The trimmed range.</returns>
    private readonly TextRange Trimmed(int start, int length)
    {
        var text = _source.Slice(start, length);
        var trimmed = text.TrimStart();
        return new(start + text.Length - trimmed.Length, trimmed.TrimEnd().Length);
    }

    /// <summary>Forgets the current component's fields.</summary>
    private void ClearFields()
    {
        _version = default;
        _origin = default;
        _copyright = default;
        _link = default;
    }
}
