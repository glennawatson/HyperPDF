// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Attachments;

/// <summary>A file embedded in a document.</summary>
/// <param name="Index">The attachment's index in the document.</param>
/// <param name="Name">The file name.</param>
/// <param name="Size">The size in bytes.</param>
[DebuggerDisplay("DocumentAttachment: {Name} ({Size} bytes)")]
public sealed record DocumentAttachment(int Index, string Name, long Size);
