// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;
using HyperPdf.Core.Protection;

namespace HyperPdf.ViewModels.Output;

/// <summary>Writes a protected or unprotected copy to a temporary file and moves it into place only when the copy was made.</summary>
internal static class ProtectedCopyFile
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    private const string TemporarySuffix = ".protect";

    /// <summary>The resource key for a destination that would overwrite the open document.</summary>
    private const string OverwriteKey = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>Gets why a chosen destination was refused.</summary>
    internal static string OverwriteMessage => UiText.Get(OverwriteKey);

    /// <summary>Gets the retained resource for a refused destination.</summary>
    internal static LocalizedMessage OverwriteStatus => LocalizedMessage.Get(UiText, OverwriteKey);

    /// <summary>Determines whether two paths name the same file.</summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    internal static bool IsSameFile(string first, string second)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    /// <summary>Writes the copy off the UI thread, then moves it over <paramref name="path"/> when it was made.</summary>
    /// <param name="write">Writes the copy to the stream it is given.</param>
    /// <param name="path">Where the copy goes.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The outcome; nothing is left at <paramref name="path"/> when the copy was refused or the run failed.</returns>
    internal static async Task<ProtectionResult> WriteAsync(Func<Stream, CancellationToken, Task<ProtectionResult>> write, string path, CancellationToken cancellationToken)
    {
        var temporary = path + TemporarySuffix;
        try
        {
            ProtectionResult result;
            await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous))
            {
                // The library reads and rewrites the whole document before its first await, so the app starts it on the
                // pool to keep the window responsive; the library cannot hop threads itself.
                result = await Task.Run(() => write(stream, cancellationToken), cancellationToken).ConfigureAwait(true);
            }

            if (!result.Written)
            {
                TryDelete(temporary);
                return result;
            }

            FileReplacement.Replace(temporary, path);
            return result;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
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
