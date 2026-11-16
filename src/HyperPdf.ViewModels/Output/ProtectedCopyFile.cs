// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>What a protected or unprotected copy needs that the other copies do not: the ending of its temporary file and the refusal for an open document.</summary>
internal static class ProtectedCopyFile
{
    /// <summary>The ending of the temporary file the copy is written to.</summary>
    internal const string TemporarySuffix = ".protect";

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
}
