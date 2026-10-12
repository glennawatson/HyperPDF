// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.Linux.Kde;

/// <summary>Supplies the KDE colour scheme from <c>kdeglobals</c>, re-reading it whenever the file changes.</summary>
[DebuggerDisplay("KdeThemeSource: {FilePath}")]
public sealed class KdeThemeSource : IDesktopThemeSource
{
    /// <summary>Initializes a new instance of the <see cref="KdeThemeSource"/> class using the user's <c>kdeglobals</c>.</summary>
    public KdeThemeSource()
        : this(Path.Combine(XdgDirectories.ConfigHome, "kdeglobals"))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KdeThemeSource"/> class.</summary>
    /// <param name="filePath">The <c>kdeglobals</c> path.</param>
    public KdeThemeSource(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
        Palette = new(Signal.Defer(() => FileChanges.Watch(filePath)
            .Select(_ => Read(filePath))
            .StartWith(Read(filePath))
            .DistinctUntilChanged()));
    }

    /// <summary>Gets the watched file.</summary>
    public string FilePath { get; }

    /// <inheritdoc/>
    public AsObservableSignal<DesktopPalette?> Palette { get; }

    /// <summary>Reads and parses the file.</summary>
    /// <param name="filePath">The file.</param>
    /// <returns>The palette or <see langword="null"/>.</returns>
    private static DesktopPalette? Read(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? KdeGlobalsParser.Parse(File.ReadAllText(filePath)) : null;
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
}
