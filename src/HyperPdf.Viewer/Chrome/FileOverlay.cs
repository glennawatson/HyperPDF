// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The file dialog, drawn inside the window so it looks and works the same on every system. It lists one folder at a time, and a
/// path can be typed instead of picked.
/// </summary>
[DebuggerDisplay("FileOverlay: {_folder}")]
internal sealed class FileOverlay : IChromeOverlay
{
    /// <summary>The rows of the list shown at once.</summary>
    private const int VisibleRows = 10;

    /// <summary>The rows of the dialog besides the list: heading, typing box, folder name and buttons.</summary>
    private const int OtherRows = 4;

    /// <summary>The mark after the name of a folder.</summary>
    private const string FolderMark = "/";

    /// <summary>The extension of the files listed.</summary>
    private const string PdfExtension = ".pdf";

    /// <summary>The names in the folder shown: folders first, then files.</summary>
    private readonly List<string> _names = [];

    /// <summary>The answer, set when the dialog closes.</summary>
    private readonly TaskCompletionSource<string?> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Whether the dialog asks where to save rather than what to open.</summary>
    private readonly bool _saving;

    /// <summary>The folder shown.</summary>
    private string _folder;

    /// <summary>The text typed: a path to open, or the name to save as.</summary>
    private string _entry;

    /// <summary>The folders at the start of <see cref="_names"/>.</summary>
    private int _folderCount;

    /// <summary>The first row of the list shown.</summary>
    private int _first;

    /// <summary>Initializes a new instance of the <see cref="FileOverlay"/> class.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <param name="saveName">The file name to offer when saving, or <see langword="null"/> to ask what to open.</param>
    internal FileOverlay(string folder, string? saveName)
    {
        _saving = saveName is not null;
        _entry = saveName ?? string.Empty;
        _folder = Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ReadFolder();
    }

    /// <inheritdoc/>
    public bool IsModal => true;

    /// <inheritdoc/>
    public bool TakesAllKeys => true;

    /// <inheritdoc/>
    public bool ClosesOnOutsidePress => false;

    /// <inheritdoc/>
    public int Version { get; private set; }

    /// <summary>Gets the answer: the path chosen, or <see langword="null"/> when the dialog was cancelled.</summary>
    internal Task<string?> Answer => _answer.Task;

    /// <inheritdoc/>
    public void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size)
    {
        var style = ChromeBars.Style(metrics);
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = width - metrics.Padding - metrics.Padding;
        var height = (metrics.RowHeight * (VisibleRows + OtherRows)) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        var title = _saving ? UiText.Get("Save a copy as") : UiText.Get("Open a file");
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, title, new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        var typed = FitEnd(_entry, style, inner - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Field, ChromeCommand.None, 0, typed, new(left + metrics.Padding, y, inner, metrics.RowHeight), true, true));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, FitEnd(_folder, style, inner, runs), new(left, y, width, metrics.RowHeight), false, false));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Row, ChromeCommand.ParentFolder, 0, UiText.Get("↑ Up one folder"), new(left, y, width, metrics.RowHeight), Directory.GetParent(_folder) is not null, false));
        y += metrics.RowHeight;
        for (var row = 0; row < VisibleRows; row++)
        {
            var index = _first + row;
            if (index < _names.Count)
            {
                var name = index < _folderCount ? string.Concat(_names[index], FolderMark) : _names[index];
                items.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, index, ChromeText.Fit(name, style, inner, runs), new(left, y, width, metrics.RowHeight), true, false));
            }

            y += metrics.RowHeight;
        }

        var buttons = new ChromeRow(items, runs, metrics, left + metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        buttons.Button(_saving ? UiText.Get("Save") : UiText.Get("Open"), ChromeCommand.Accept, _entry.Length > 0);
        buttons.Button(UiText.Get("Cancel"), ChromeCommand.Cancel, true);
    }

    /// <inheritdoc/>
    public bool Press(ChromeItem item)
    {
        switch (item.Command)
        {
            case ChromeCommand.ParentFolder when Directory.GetParent(_folder) is { } parent:
                {
                    ShowFolder(parent.FullName);
                    return true;
                }

            case ChromeCommand.PickRow:
                {
                    return Pick(item.Index);
                }

            case ChromeCommand.Accept:
                {
                    return AcceptEntry();
                }

            case ChromeCommand.Cancel:
                {
                    _ = _answer.TrySetResult(null);
                    return false;
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <inheritdoc/>
    public bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Enter:
                {
                    return AcceptEntry();
                }

            case ViewerKey.Escape:
                {
                    _ = _answer.TrySetResult(null);
                    return false;
                }

            case ViewerKey.Back when _entry.Length > 0:
                {
                    var cut = char.IsLowSurrogate(_entry[^1]) && _entry.Length > 1 ? _entry.Length - 1 - 1 : _entry.Length - 1;
                    _entry = _entry[..cut];
                    Version++;
                    return true;
                }

            case ViewerKey.Down or ViewerKey.PageDown:
                {
                    Scroll(key == ViewerKey.Down ? 1 : VisibleRows);
                    return true;
                }

            case ViewerKey.Up or ViewerKey.PageUp:
                {
                    Scroll(key == ViewerKey.Up ? -1 : -VisibleRows);
                    return true;
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <inheritdoc/>
    public void Type(int scalar)
    {
        if (!Rune.TryCreate(scalar, out var rune) || Rune.IsControl(rune))
        {
            return;
        }

        _entry = string.Concat(_entry, rune.ToString());
        Version++;
    }

    /// <inheritdoc/>
    public void Scroll(int rows)
    {
        var first = Math.Clamp(_first + rows, 0, Math.Max(0, _names.Count - VisibleRows));
        if (first == _first)
        {
            return;
        }

        _first = first;
        Version++;
    }

    /// <inheritdoc/>
    public void Dismiss() => _ = _answer.TrySetResult(null);

    /// <summary>Cuts text short at its start so its end fits a width. The end of a path is the part that tells paths apart.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it must fit.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The text, whole when it fits.</returns>
    private static string FitEnd(string text, TextStyle style, int width, TextRuns runs)
    {
        var start = 0;
        while (start < text.Length && runs.Measure(text.AsSpan(start), style) > width)
        {
            start++;
        }

        if (start < text.Length && char.IsLowSurrogate(text[start]))
        {
            start++;
        }

        return start == 0 ? text : text[start..];
    }

    /// <summary>Handles a press on a row of the list.</summary>
    /// <param name="index">The row.</param>
    /// <returns><see langword="true"/> when the dialog stays open.</returns>
    private bool Pick(int index)
    {
        var path = Path.Combine(_folder, _names[index]);
        if (index < _folderCount)
        {
            ShowFolder(path);
            return true;
        }

        if (_saving)
        {
            _entry = _names[index];
            Version++;
            return true;
        }

        _ = _answer.TrySetResult(path);
        return false;
    }

    /// <summary>Uses the typed text: goes into a folder, or answers with a file.</summary>
    /// <returns><see langword="true"/> when the dialog stays open.</returns>
    private bool AcceptEntry()
    {
        if (_entry.Length == 0)
        {
            return true;
        }

        var path = Path.GetFullPath(_entry, _folder);
        if (Directory.Exists(path))
        {
            _entry = string.Empty;
            ShowFolder(path);
            return true;
        }

        if (!_saving && !File.Exists(path))
        {
            return true;
        }

        _ = _answer.TrySetResult(path);
        return false;
    }

    /// <summary>Shows another folder.</summary>
    /// <param name="folder">The folder.</param>
    private void ShowFolder(string folder)
    {
        _folder = folder;
        _first = 0;
        ReadFolder();
        Version++;
    }

    /// <summary>Reads the names in the folder shown. A folder that cannot be read is shown empty.</summary>
    private void ReadFolder()
    {
        _names.Clear();
        _folderCount = 0;
        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            foreach (var path in Directory.EnumerateDirectories(_folder, "*", options))
            {
                _names.Add(Path.GetFileName(path));
            }

            _names.Sort(StringComparer.CurrentCultureIgnoreCase);
            _folderCount = _names.Count;
            foreach (var path in Directory.EnumerateFiles(_folder, "*", options))
            {
                if (path.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    _names.Add(Path.GetFileName(path));
                }
            }

            _names.Sort(_folderCount, _names.Count - _folderCount, StringComparer.CurrentCultureIgnoreCase);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _names.Clear();
            _folderCount = 0;
        }
    }
}
