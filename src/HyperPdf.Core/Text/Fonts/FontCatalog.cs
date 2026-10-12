// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// The fonts installed on this computer that text can be written in. Faces are found by reading the font folders of
/// Linux, Windows and macOS directly, so the same code runs everywhere. Only faces that can be embedded in a PDF are
/// offered; the three built in PDF families are always offered first.
/// </summary>
[DebuggerDisplay("FontCatalog: {Families.Count} families")]
public sealed class FontCatalog
{
    /// <summary>The regular weight.</summary>
    private const int RegularWeight = 400;

    /// <summary>The bold weight.</summary>
    private const int BoldWeight = 700;

    /// <summary>The weight distance that outweighs any slant difference, so slant matches first.</summary>
    private const int SlantPenalty = 1000;

    /// <summary>The system catalog, scanned on first use.</summary>
    private static readonly Lazy<FontCatalog> SystemCatalog = new(static () => Scan(SystemDirectories()), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The embeddable faces by family name.</summary>
    private readonly Dictionary<string, List<FontFace>> _byFamily;

    /// <summary>The first face of each family that cannot be embedded, by family name.</summary>
    private readonly Dictionary<string, FontFace> _previewOnly = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Initializes a new instance of the <see cref="FontCatalog"/> class.</summary>
    /// <param name="faces">The faces.</param>
    public FontCatalog(IEnumerable<FontFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        _byFamily = [with(StringComparer.OrdinalIgnoreCase)];
        var all = new List<FontFace>();
        foreach (var face in faces)
        {
            if (StandardFontFamilies.IsStandard(face.Family))
            {
                continue;
            }

            if (!face.CanEmbed)
            {
                _ = _previewOnly.TryAdd(face.Family, face);
                continue;
            }

            all.Add(face);
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_byFamily, face.Family, out _);
            list ??= [];
            list.Add(face);
        }

        Faces = all;
        var installed = _byFamily.Keys.Order(StringComparer.OrdinalIgnoreCase);
        Families = [.. StandardFontFamilies.All, .. installed];
        foreach (var family in _byFamily.Keys)
        {
            _ = _previewOnly.Remove(family);
        }

        PreviewOnlyFamilies = [.. _previewOnly.Keys.Order(StringComparer.OrdinalIgnoreCase)];
        var every = new List<string>(_byFamily.Keys);
        every.AddRange(PreviewOnlyFamilies);
        every.Sort(StringComparer.OrdinalIgnoreCase);
        AllFamilies = [.. StandardFontFamilies.All, .. every];
    }

    /// <summary>Gets the installed fonts, read on first use; reading is quick but touches the disk, so call it off the UI thread first.</summary>
    public static FontCatalog System => SystemCatalog.Value;

    /// <summary>Gets a value indicating whether the installed fonts have been read, so <see cref="System"/> returns at once.</summary>
    public static bool IsSystemLoaded => SystemCatalog.IsValueCreated;

    /// <summary>Gets a catalog with only the built in families.</summary>
    public static FontCatalog Empty { get; } = new([]);

    /// <summary>Gets the families offered: the built in three, then installed families in name order.</summary>
    public IReadOnlyList<string> Families { get; }

    /// <summary>
    /// Gets the installed families none of whose faces can be embedded, because their licence forbids it or their
    /// outlines are not TrueType. Text in them can be shown on screen, and is saved in the closest built in family.
    /// </summary>
    public IReadOnlyList<string> PreviewOnlyFamilies { get; }

    /// <summary>Gets the built in families, then every installed family, preview only ones included, in name order.</summary>
    public IReadOnlyList<string> AllFamilies { get; }

    /// <summary>Gets every embeddable installed face.</summary>
    public IReadOnlyList<FontFace> Faces { get; }

    /// <summary>Gets the folders fonts are installed in on this operating system, for every user and for this user.</summary>
    /// <returns>The folders that exist.</returns>
    public static IReadOnlyList<string> SystemDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates;
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates = [Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Path.Combine(local, "Microsoft", "Windows", "Fonts")];
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates = ["/System/Library/Fonts", "/Library/Fonts", Path.Combine(home, "Library", "Fonts")];
        }
        else
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(home, ".local", "share");
            candidates = ["/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(dataHome, "fonts"), Path.Combine(home, ".fonts")];
        }

        var folders = new List<string>(candidates.Length);
        foreach (var folder in candidates)
        {
            if (folder.Length > 0 && Directory.Exists(folder) && !folders.Contains(folder))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }

    /// <summary>Reads every font file under some folders. Files that cannot be read are skipped.</summary>
    /// <param name="directories">The folders, searched with their sub-folders.</param>
    /// <returns>The catalog.</returns>
    public static FontCatalog Scan(IEnumerable<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        var faces = new List<FontFace>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System };
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*", options))
            {
                if (IsFontFile(path))
                {
                    ReadFile(path, faces);
                }
            }
        }

        return new(faces);
    }

    /// <summary>Determines whether a family is offered.</summary>
    /// <param name="family">The family.</param>
    /// <returns><see langword="true"/> for the built in families and installed embeddable ones.</returns>
    public bool Contains(string family) => StandardFontFamilies.IsStandard(family) || _byFamily.ContainsKey(family);

    /// <summary>Determines whether a family is installed but can only be shown on screen, not saved.</summary>
    /// <param name="family">The family.</param>
    /// <returns><see langword="true"/> for preview only families.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPreviewOnly(string family) => _previewOnly.ContainsKey(family);

    /// <summary>Gets a face of a preview only family, which says whether it has serifs or fixed widths.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The face, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FontFace? PreviewFace(string family) => _previewOnly.GetValueOrDefault(family);

    /// <summary>Finds the installed face for a family and style: the nearest weight with the right slant.</summary>
    /// <param name="family">The family.</param>
    /// <param name="bold">Whether bold is wanted.</param>
    /// <param name="italic">Whether italic is wanted.</param>
    /// <returns>The match, or <see langword="null"/> when the family is not installed or is a built in one.</returns>
    public FontMatch? Find(string family, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(family) || !_byFamily.TryGetValue(family, out var faces))
        {
            return null;
        }

        var target = bold ? BoldWeight : RegularWeight;
        FontFace? best = null;
        var bestScore = int.MaxValue;
        foreach (var face in faces)
        {
            var score = Math.Abs(face.Weight - target) + (face.IsItalic == italic ? 0 : SlantPenalty);
            if (score < bestScore)
            {
                (best, bestScore) = (face, score);
            }
        }

        return best is null ? null : new(best, bold && !best.IsBold, italic && !best.IsItalic);
    }

    /// <summary>Determines whether a file name is a TrueType or OpenType font or collection.</summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> for font files.</returns>
    private static bool IsFontFile(string path)
    {
        var extension = Path.GetExtension(path.AsSpan());
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otc", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads one font file, skipping it when it cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <param name="faces">Receives its faces.</param>
    private static void ReadFile(string path, List<FontFace> faces)
    {
        try
        {
            FontFaceReader.Read(path, faces);
        }
        catch (IOException)
        {
            // A font that disappears or is locked while scanning is skipped.
        }
        catch (UnauthorizedAccessException)
        {
            // A font the user cannot read is skipped.
        }
    }
}
