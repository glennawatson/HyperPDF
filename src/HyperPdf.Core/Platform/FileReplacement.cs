// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Platform;

/// <summary>
/// Moves a newly written file over an existing one. Unix and macOS replace the name in one step and anything still
/// reading the old file keeps it. Windows can refuse to replace a file that is open, even one opened to allow it, so
/// there the old file is first renamed aside, the new one moved in and the old one deleted once nothing needs it.
/// </summary>
public static class FileReplacement
{
    /// <summary>Moves <paramref name="source"/> over <paramref name="destination"/>.</summary>
    /// <param name="source">The new file.</param>
    /// <param name="destination">The file to replace, which need not exist.</param>
    /// <exception cref="IOException">The file could not be replaced; <paramref name="destination"/> is left as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to either file was refused; <paramref name="destination"/> is left as it was.</exception>
    public static void Replace(string source, string destination)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(destination);
        try
        {
            File.Move(source, destination, true);
        }
        catch (Exception ex) when (OperatingSystem.IsWindows() && File.Exists(destination) && ex is IOException or UnauthorizedAccessException)
        {
            ReplaceAside(source, destination);
        }
    }

    /// <summary>Renames the old file aside, moves the new one in and deletes the old one, putting it back on failure.</summary>
    /// <param name="source">The new file.</param>
    /// <param name="destination">The file to replace.</param>
    private static void ReplaceAside(string source, string destination)
    {
        var aside = $"{destination}.{Guid.NewGuid():N}.old";
        File.Move(destination, aside);
        try
        {
            File.Move(source, destination);
        }
        catch
        {
            File.Move(aside, destination);
            throw;
        }

        try
        {
            // An open file is deleted once its last reader closes it.
            File.Delete(aside);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
