// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Tells picture files, which the viewer opens as one page documents, from PDFs.</summary>
public static class PictureFiles
{
    /// <summary>The file name endings of the pictures the viewer opens.</summary>
    private static readonly string[] Endings = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff"];

    /// <summary>Gets the file name patterns of the pictures the viewer opens, for a file dialog.</summary>
    public static IReadOnlyList<string> Patterns { get; } = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.tif", "*.tiff"];

    /// <summary>Determines whether a file name is one of the pictures the viewer opens.</summary>
    /// <param name="path">The file path or name.</param>
    /// <returns><see langword="true"/> when the ending names a picture.</returns>
    public static bool IsPictureFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var ending in Endings)
        {
            if (path.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Makes the name suggested when a picture opened as a document is saved, which is a PDF.</summary>
    /// <param name="fileName">The picture's file name.</param>
    /// <returns>The same name ending in <c>.pdf</c>.</returns>
    public static string SuggestedPdfName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return Path.ChangeExtension(fileName, ".pdf");
    }
}
