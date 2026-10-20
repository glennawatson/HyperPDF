// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Licences;

/// <summary>The licences that components shipped with the app may use.</summary>
public static class LicenceAllowList
{
    /// <summary>
    /// The SPDX ids allowed: MIT, BSD, Apache-2.0, OFL-1.1, Zlib, ISC, Unicode, MS-PL, CC-BY-4.0 for data that needs
    /// only attribution, such as the veraPDF rule descriptions, and LGPL-3.0-or-later for the FFmpeg shared libraries,
    /// which are separate files beside the app that a reader can replace.
    /// </summary>
    private static readonly HashSet<string> Allowed =
    [
        with(StringComparer.Ordinal),
        "CC-BY-4.0",
        "MIT",
        "BSD-2-Clause",
        "BSD-3-Clause",
        "Apache-2.0",
        "OFL-1.1",
        "Zlib",
        "ISC",
        "Unicode-3.0",
        "Unicode-DFS-2016",
        "MS-PL",
        "LGPL-3.0-or-later",
    ];

    /// <summary>Gets the allowed SPDX ids.</summary>
    public static IReadOnlyCollection<string> Ids => Allowed;

    /// <summary>
    /// Determines whether a licence expression only uses allowed licences. Every id in an expression counts, so
    /// <c>MIT AND Apache-2.0</c> passes and <c>MIT OR GPL-3.0-only</c> does not.
    /// </summary>
    /// <param name="expression">The SPDX expression.</param>
    /// <returns><see langword="true"/> when every id is allowed.</returns>
    public static bool IsAllowed(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var found = false;
        foreach (var word in expression.Split([' ', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word is "AND" or "OR" or "WITH")
            {
                continue;
            }

            if (!Allowed.Contains(word))
            {
                return false;
            }

            found = true;
        }

        return found;
    }
}
