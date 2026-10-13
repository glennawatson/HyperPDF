// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;
using Avalonia.Platform.Storage;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>Operators for dropped files and web addresses.</summary>
internal static class DragDropMixins
{
    /// <summary>The characters that separate dropped web addresses.</summary>
    private static readonly char[] LineBreaks = ['\r', '\n'];

    /// <summary>Adds the effect a drag is offered.</summary>
    /// <param name="args">The drag event.</param>
    extension(DragEventArgs args)
    {
        /// <summary>Gets the effect for a drag: copy when it carries files or text, otherwise none.</summary>
        internal DragDropEffects CopyFilesAndText => args.DataTransfer.Contains(DataFormat.File) || args.DataTransfer.Contains(DataFormat.Text)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        /// <summary>Gets the local paths of the dropped files, else the web addresses in dropped text, else <see langword="null"/>.</summary>
        internal List<string>? DroppedPaths
        {
            get
            {
                if (args.DataTransfer.TryGetFiles() is { Length: > 0 } files)
                {
                    return files.LocalPaths;
                }

                return args.DataTransfer.TryGetText() is { Length: > 0 } text
                    ? [.. text.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
                    : null;
            }
        }
    }

    /// <summary>Adds the path conversion of storage items.</summary>
    /// <param name="items">The items.</param>
    extension(IEnumerable<IStorageItem> items)
    {
        /// <summary>Gets the local paths of the items that have one.</summary>
        internal List<string> LocalPaths
        {
            get
            {
                var paths = new List<string>();
                foreach (var item in items)
                {
                    if (item.TryGetLocalPath() is { } path)
                    {
                        paths.Add(path);
                    }
                }

                return paths;
            }
        }
    }

    /// <summary>Adds the drop filters.</summary>
    /// <param name="drops">The drop events.</param>
    extension(IObservable<DragEventArgs> drops)
    {
        /// <summary>Gets the paths or web addresses each drop carries; a drop with neither gives nothing.</summary>
        internal IObservable<List<string>> Dropped => drops.Select(static args => args.DroppedPaths).KeepNotNull();
    }
}
