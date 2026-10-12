// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>The text layout and optional logical structure from one opened document.</summary>
/// <param name="Characters">The document's character layout.</param>
/// <param name="Structure">The document's logical structure, when supported.</param>
[DebuggerDisplay("ReadingSources: {Characters}, {Structure}")]
public readonly record struct ReadingSources(ITextLayoutSource Characters, ITaggedStructureSource? Structure);
