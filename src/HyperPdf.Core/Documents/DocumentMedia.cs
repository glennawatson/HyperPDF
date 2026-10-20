// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A piece of embedded sound or video, which the reader can save or hand to the system player. It never plays by itself.</summary>
/// <param name="Index">The position in the document's list of media.</param>
/// <param name="Kind">The kind of annotation that holds it.</param>
/// <param name="PageIndex">The zero based page of the annotation.</param>
/// <param name="Name">A label in plain words, such as "intro.mp4 (video on page 2)".</param>
/// <param name="FileName">The name to suggest when saving it.</param>
/// <param name="ContentType">The media type, such as <c>video/mp4</c>.</param>
/// <param name="Size">The size in bytes of the data in the file, or 0 when it is not known.</param>
[DebuggerDisplay("DocumentMedia: {Name} ({Size} bytes)")]
public sealed record DocumentMedia(int Index, DocumentMediaKind Kind, int PageIndex, string Name, string FileName, string ContentType, long Size);
