// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Forms;

namespace HyperPdf.ViewModels.FormFilling;

/// <summary>Reads and describes the FDF and XFDF files a form's Import Data action loads, treating the form and the file as untrusted.</summary>
internal static class FormDataFiles
{
    /// <summary>The largest file read, 16 MiB. Real answer files are a few kilobytes.</summary>
    internal const long MaxBytes = 16_777_216;

    /// <summary>The longest file name shown to the reader.</summary>
    private const int MaxDisplayLength = 80;

    /// <summary>Gets the name shown when the form names no file.</summary>
    private static string UnnamedFile => UiText.Get("a data file");

    /// <summary>Gets a name safe to show in a question: the last part of the path the form gave, without control characters, cut to a sensible length.</summary>
    /// <param name="file">The file as the form names it, or <see langword="null"/>.</param>
    /// <returns>The name, or a plain phrase when the form names no file.</returns>
    internal static string DisplayName(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return UnnamedFile;
        }

        var name = file.AsSpan(file.AsSpan().LastIndexOfAny('/', '\\') + 1);
        var text = new StringBuilder(Math.Min(name.Length, MaxDisplayLength));
        foreach (var character in name)
        {
            if (text.Length == MaxDisplayLength)
            {
                break;
            }

            _ = text.Append(char.IsControl(character) ? ' ' : character);
        }

        var shown = text.ToString().Trim();
        return shown.Length == 0 ? UnnamedFile : shown;
    }

    /// <summary>Reads a file the reader chose, refusing one that is too long or cannot be read.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or <see langword="null"/> when the file is too long or cannot be read.</returns>
    internal static async ValueTask<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaxBytes)
            {
                return null;
            }

            var data = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
            return data;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Describes what an import did, in plain words.</summary>
    /// <param name="name">The file's name.</param>
    /// <param name="result">The result.</param>
    /// <returns>The notice.</returns>
    internal static string Describe(string name, FormDataImportResult result)
    {
        if (!result.Succeeded)
        {
            return UiText.Format("{0} is not a valid FDF or XFDF file, or is too large. The form is unchanged.", name);
        }

        var applied = result.FieldsApplied + result.AnnotationsApplied;
        var skipped = result.FieldsSkipped + result.AnnotationsSkipped;
        var summary = applied == 0
            ? UiText.Format(
            "{0} had nothing that fits this form.",
            name)
            : UiText.Format("Imported {0} {1} from {2}.", applied, (applied == 1 ? UiText.Get("entry") : UiText.Get("entries")), name);
        return skipped == 0 ? summary : UiText.Format("{0} {1} {2} ignored.", summary, skipped, (skipped == 1 ? UiText.Get("entry was") : UiText.Get("entries were")));
    }
}
