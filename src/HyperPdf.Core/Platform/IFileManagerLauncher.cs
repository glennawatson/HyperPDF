// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Platform;

/// <summary>Opens the desktop file manager.</summary>
public interface IFileManagerLauncher
{
    /// <summary>Opens the folder containing a file with the file selected.</summary>
    /// <param name="filePath">The file.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the file manager accepted the request; unsubscribing cancels the request.</returns>
    IObservable<bool> ShowItem(string filePath);
}
