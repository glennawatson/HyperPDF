// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads PDF file specifications.</summary>
public static class PdfDocumentFileSpecs
{
    /// <summary>The position of the drive letter's closing slash in "/c/dir/file".</summary>
    private const int DriveSlot = 2;

    /// <summary>The most bytes kept of a script stream.</summary>
    private const int MaxScriptLength = 1 << 20;

    /// <summary>Gets the spelling of the /DOS file specification key.</summary>
    private static ReadOnlySpan<byte> DosSpelling => "DOS"u8;

    /// <summary>Gets the spelling of the /FS file system key.</summary>
    private static ReadOnlySpan<byte> FsSpelling => "FS"u8;

    /// <summary>Gets the spelling of the /URL file system name.</summary>
    private static ReadOnlySpan<byte> UrlSpelling => "URL"u8;

    /// <summary>
    /// Converts a path written in PDF file specification syntax to the platform's. On Windows "/c/dir/file" becomes
    /// "c:\dir\file" and "//server/share" becomes "\\server\share"; elsewhere the syntax is already the platform's.
    /// </summary>
    /// <param name="path">The path as written in the file specification.</param>
    /// <returns>The platform path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToPlatformPath(string path) => PdfDocumentFileSpecs.ToPlatformPath(path, OperatingSystem.IsWindows());

    /// <summary>Converts a file specification path to Windows or POSIX syntax.</summary>
    /// <param name="path">The path as written in the file specification.</param>
    /// <param name="windows">Whether to produce a Windows path.</param>
    /// <returns>The path.</returns>
    internal static string ToPlatformPath(string path, bool windows)
    {
        if (!windows || path.Length == 0)
        {
            return path;
        }

        var converted = path.Replace('/', '\\');

        // "//server/share" is already a UNC path once the slashes are flipped.
        if (path[0] != '/' || (path.Length > 1 && path[1] == '/'))
        {
            return converted;
        }

        return path.Length > PdfDocumentFileSpecs.DriveSlot && path[PdfDocumentFileSpecs.DriveSlot] == '/' ? string.Concat(
path.AsSpan(1, 1),
":",
converted.AsSpan(PdfDocumentFileSpecs.DriveSlot)) : converted;
    }

    /// <summary>Reads a script held as a string or a stream, keeping at most <see cref="MaxScriptLength"/> bytes of a stream.</summary>
    /// <param name="value">The /JS value.</param>
    /// <returns>The script, or <see langword="null"/>.</returns>
    internal static string? ReadScript(PdfValue value)
    {
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        return value.AsStream() is { } stream ? PdfDocumentFileSpecs.DecodeScript(stream) : null;
    }

    /// <summary>Reads a file specification: a string, or a dictionary preferring /UF, then /F, /DOS, /Mac and /Unix.</summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The file specification.</param>
    /// <returns>The file name as written, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string? ReadFileSpec(PdfDocument document, PdfValue value) => PdfDocumentFileSpecs.ReadFileSpec(document, value, out _);

    /// <summary>Reads a file specification, noting whether it is a URL (/FS /URL).</summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The file specification.</param>
    /// <param name="isUrl">Whether the specification names a URL rather than a file.</param>
    /// <returns>The file name or URL as written, or <see langword="null"/>.</returns>
    internal static string? ReadFileSpec(PdfDocument document, PdfValue value, out bool isUrl)
    {
        isUrl = false;
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        if (value.AsDictionary() is not { } spec)
        {
            return null;
        }

        isUrl = PdfDocumentFileSpecs.IsUrlSpec(document, spec);
        var name = spec.GetText(KnownName.UF);
        name = string.IsNullOrEmpty(name) ? spec.GetText(KnownName.F) : name;
        if (string.IsNullOrEmpty(name) && !isUrl)
        {
            name = spec.GetText(document.Objects.Names.Intern(PdfDocumentFileSpecs.DosSpelling)) ?? spec.GetText(KnownName.Mac) ?? spec.GetText(KnownName.Unix);
        }

        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>Reads a URI action; a URI without a scheme is resolved against the catalog's /URI /Base (ISO 32000-2 12.6.4.7).</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    internal static UriAction ReadUri(PdfDocument document, PdfDictionary action)
    {
        var uri = action.GetStringBytes(KnownName.URI);
        var text = Encoding.UTF8.GetString(uri);
        var hasScheme = uri.IndexOf((byte)':') >= 1;
        return hasScheme || document.Catalog.GetDictionary(KnownName.URI) is not { } settings ? new(text) : new(Encoding.UTF8.GetString(settings.GetStringBytes(KnownName.Base)) + text);
    }

    /// <summary>Reads a launch action, preferring /F and then the Windows /Win /F.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the file is missing.</returns>
    internal static PdfAction ReadLaunch(PdfDocument document, PdfDictionary action)
    {
        var file = PdfDocumentFileSpecs.ReadFileSpec(document, action.Get(KnownName.F), out var isUrl) ?? PdfDocumentFileSpecs.ReadFileSpec(
document,
action.GetDictionary(KnownName.Win)?.Get(KnownName.F) ?? default,
out isUrl);
        if (file is null)
        {
            return default;
        }

        return isUrl ? new UriAction(file) : new LaunchAction(PdfDocumentFileSpecs.ToPlatformPath(file));
    }

    /// <summary>Decodes a script stream and keeps its first <see cref="MaxScriptLength"/> bytes.</summary>
    /// <param name="stream">The script stream.</param>
    /// <returns>The script.</returns>
    private static string DecodeScript(PdfStream stream)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref output);
            return PdfText.Decode(output.WrittenSpan[..Math.Min(output.Length, PdfDocumentFileSpecs.MaxScriptLength)]);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Determines whether a file specification dictionary has /FS /URL.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The file specification.</param>
    /// <returns><see langword="true"/> for a URL.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUrlSpec(PdfDocument document, PdfDictionary spec) => document.Objects.Names.NameEquals(
spec.GetName(document.Objects.Names.Intern(PdfDocumentFileSpecs.FsSpelling)),
PdfDocumentFileSpecs.UrlSpelling);
}
