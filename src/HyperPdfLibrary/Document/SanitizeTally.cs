// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Counts what a sanitising pass takes out while it runs.</summary>
[DebuggerDisplay("SanitizeTally: {Scripts} scripts, {Attachments} attachments, {Comments} comments")]
internal sealed class SanitizeTally
{
    /// <summary>Gets or sets the number of scripts and actions that run them removed.</summary>
    internal int Scripts { get; set; }

    /// <summary>Gets or sets the number of embedded files and file attachment notes removed.</summary>
    internal int Attachments { get; set; }

    /// <summary>Gets or sets the number of notes and marks removed.</summary>
    internal int Comments { get; set; }
}
