// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>What one apply of the tag form has changed so far, and whether the document refused a change.</summary>
internal sealed class TagEditRun
{
    /// <summary>Gets the number of changes the document accepted.</summary>
    internal int Changed { get; private set; }

    /// <summary>Gets or sets a value indicating whether a change was refused.</summary>
    internal bool Refused { get; set; }

    /// <summary>Counts a change the document accepted, or records that it refused one.</summary>
    /// <param name="changed">Whether the document changed.</param>
    internal void Record(bool changed)
    {
        if (changed)
        {
            Changed++;
            return;
        }

        Refused = true;
    }
}
