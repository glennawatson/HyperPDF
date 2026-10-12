// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Annotations;

/// <summary>A reply to a comment, or a change of its review status.</summary>
/// <param name="Index">The reply's annotation index on the page.</param>
/// <param name="Contents">The reply's text; empty for a bare status change.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="State">The review status it sets, or <see cref="ReviewState.None"/> for a plain reply.</param>
[DebuggerDisplay("AnnotationReply: {Author}: {Contents} ({State})")]
public sealed record AnnotationReply(int Index, string Contents, string Author, ReviewState State);
