// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The text of a tag that can be changed.</summary>
public enum DocumentTagField
{
    /// <summary>The description a screen reader says in place of the content.</summary>
    AlternateText = 0,

    /// <summary>The text that replaces the content when it is read or copied.</summary>
    ActualText = 1,

    /// <summary>The words an abbreviation stands for.</summary>
    Expansion = 2,

    /// <summary>The tag's title.</summary>
    Title = 3,

    /// <summary>The language of the tag's content, such as en-AU.</summary>
    Language = 4,
}
