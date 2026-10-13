// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Protection;

/// <summary>What a person who opens a protected file with the reading password may do. A set flag allows the action.</summary>
[Flags]
public enum ProtectionPermissions
{
    /// <summary>Nothing is allowed.</summary>
    None = 0,

    /// <summary>Print the document.</summary>
    Print = 1 << 0,

    /// <summary>Copy text and pictures out of the document.</summary>
    Copy = 1 << 1,

    /// <summary>Change the pages and their content.</summary>
    Edit = 1 << 2,

    /// <summary>Fill in forms and add comments.</summary>
    Forms = 1 << 3,

    /// <summary>Let screen readers and other assistive tools read the text.</summary>
    Accessibility = 1 << 4,

    /// <summary>Allows every action.</summary>
    All = Print | Copy | Edit | Forms | Accessibility,
}
