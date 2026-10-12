// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Finds the files that links and attachments point to, and decides how they may open: another PDF opens in a tab,
/// a program or script never runs, and anything else opens with the desktop's app only after the reader agrees.
/// </summary>
public static class LinkedFiles
{
    /// <summary>The scheme that marks a file URI.</summary>
    private const string FileScheme = "file:";

    /// <summary>Where the folder path starts after a PDF drive path's "/C".</summary>
    private const int DriveFolderStart = 2;

    /// <summary>The extensions of files that run code, which links and attachments are never allowed to start.</summary>
    private static readonly FrozenSet<string> Runnable = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        [
        ".app", ".apk", ".appimage", ".application", ".bat", ".bin", ".cmd", ".com", ".command", ".cpl", ".csh", ".deb", ".desktop", ".dll",
        ".dmg", ".exe", ".gadget", ".hta", ".inf", ".ins", ".isp", ".jar", ".js", ".jse", ".ksh", ".lnk", ".msc", ".msi", ".msp", ".pif",
        ".pkg", ".pl", ".ps1", ".ps2", ".psc1", ".py", ".rb", ".reg", ".rpm", ".run", ".scf", ".scr", ".sct", ".sh", ".so", ".url",
        ".vb", ".vbe", ".vbs", ".workflow", ".ws", ".wsc", ".wsf", ".wsh", ".zsh",
        ]);

    /// <summary>Gets the full path of a file named in a document.</summary>
    /// <param name="documentPath">The document that names the file.</param>
    /// <param name="linkPath">The path as written: relative to the document, absolute, a PDF-style "/C/folder/file" path or a file URI.</param>
    /// <returns>The full path, or <see langword="null"/> when it cannot be a local file.</returns>
    public static string? Resolve(string documentPath, string linkPath)
    {
        ArgumentNullException.ThrowIfNull(documentPath);
        if (string.IsNullOrWhiteSpace(linkPath))
        {
            return null;
        }

        if (linkPath.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(linkPath, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : null;
        }

        if (linkPath.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var path = ToNativeSeparators(linkPath);
        if (OperatingSystem.IsWindows() && IsPdfDrivePath(linkPath))
        {
            path = string.Concat(linkPath.AsSpan(1, 1), ":", ToNativeSeparators(linkPath[DriveFolderStart..]));
        }

        try
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(documentPath)) ?? string.Empty;
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(folder, path));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Determines whether a file is a PDF, which opens in a tab.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> for a .pdf file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPdf(string path) => Path.GetExtension(path.AsSpan()).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Determines whether a file runs code, so a link or attachment must never start it.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> for programs, scripts, installers and shortcuts.</returns>
    public static bool IsRunnable(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var extension = Path.GetExtension(path.AsSpan());
        return extension.IsEmpty || Runnable.GetAlternateLookup<ReadOnlySpan<char>>().Contains(extension);
    }

    /// <summary>Uses this system's folder separator, copying the path only when it holds the other one.</summary>
    /// <param name="path">A path with either separator.</param>
    /// <returns>The path.</returns>
    private static string ToNativeSeparators(string path)
    {
        var foreign = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        return path.Contains(foreign, StringComparison.Ordinal) ? path.Replace(foreign, Path.DirectorySeparatorChar) : path;
    }

    /// <summary>Determines whether a path is a PDF file specification's drive form, such as "/C/folder/file.pdf".</summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> for the drive form.</returns>
    private static bool IsPdfDrivePath(string path) => path.Length > DriveFolderStart && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[DriveFolderStart] == '/';
}
