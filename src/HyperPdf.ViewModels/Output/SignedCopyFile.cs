// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Output;

/// <summary>Writes a signed, updated or timestamped copy to disk and describes what the signer told the user.</summary>
internal static class SignedCopyFile
{
    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".signing";

    /// <summary>Determines whether two paths name the same file.</summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    internal static bool IsSameFile(string first, string second)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    /// <summary>Writes the outcome's file to a temporary file, then moves it over the destination.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task.</returns>
    internal static async Task WriteAsync(SigningOutcome outcome, string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var temporary = path + TemporarySuffix;
        try
        {
            await File.WriteAllBytesAsync(temporary, outcome.Bytes, cancellationToken).ConfigureAwait(false);
            FileReplacement.Replace(temporary, path);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Describes a finished run, one point per line: where the copy is saved, then each note the signer gave.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(SigningOutcome outcome, string path)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var lines = new string[outcome.Notes.Count + 1];
        lines[0] = UiText.Format("Saved as {0}.", Path.GetFileName(path));
        for (var i = 0; i < outcome.Notes.Count; i++)
        {
            lines[i + 1] = $"• {outcome.Notes[i]}";
        }

        return lines;
    }

    /// <summary>Joins the notes of an outcome into sentences that follow a message, or gives nothing when there are none.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>A space, then the notes; empty when there are no notes.</returns>
    internal static string NotesSuffix(SigningOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Notes.Count == 0 ? string.Empty : $" {string.Join(' ', outcome.Notes)}";
    }

    /// <summary>Deletes a temporary file, ignoring a failure because the file is only a leftover.</summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temporary file does no harm and a later save replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }
}
