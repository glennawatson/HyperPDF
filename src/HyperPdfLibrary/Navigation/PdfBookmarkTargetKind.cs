// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Navigation;

/// <summary>What a bookmark does when it is chosen.</summary>
public enum PdfBookmarkTargetKind
{
    /// <summary>It leads nowhere; it only groups the bookmarks below it.</summary>
    None = 0,

    /// <summary>It goes to a position on a page (<c>/Dest</c> with an explicit destination).</summary>
    Page = 1,

    /// <summary>It goes to a named destination (<c>/Dest</c> with a name).</summary>
    NamedDestination = 2,

    /// <summary>It opens a web address (a <c>/URI</c> action).</summary>
    Uri = 3,

    /// <summary>It runs a standard viewer command such as <c>NextPage</c> (a <c>/Named</c> action).</summary>
    NamedAction = 4,
}
