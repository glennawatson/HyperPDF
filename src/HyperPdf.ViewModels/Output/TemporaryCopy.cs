// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Platform;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The temporary file a copy is written to. It is moved over the destination only when the copy was made, and deleted when the
/// copy was refused, failed or was stopped, even while the write was still running.
/// </summary>
internal sealed class TemporaryCopy : IDisposable
{
    /// <summary>The temporary file.</summary>
    private readonly string _temporary;

    /// <summary>Whether the file was moved into place, so there is nothing left to delete.</summary>
    private bool _moved;

    /// <summary>Initializes a new instance of the <see cref="TemporaryCopy"/> class and creates the file.</summary>
    /// <param name="temporary">The temporary file.</param>
    internal TemporaryCopy(string temporary)
    {
        _temporary = temporary;
        Stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.Asynchronous);
    }

    /// <summary>Gets the stream the copy is written to.</summary>
    internal FileStream Stream { get; }

    /// <summary>Closes the file and deletes it unless it was moved into place.</summary>
    public void Dispose()
    {
        Stream.Dispose();
        if (_moved)
        {
            return;
        }

        try
        {
            File.Delete(_temporary);
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

    /// <summary>Closes the file and moves it over the destination.</summary>
    /// <param name="path">The destination.</param>
    internal void MoveTo(string path)
    {
        Stream.Dispose();
        FileReplacement.Replace(_temporary, path);
        _moved = true;
    }
}
