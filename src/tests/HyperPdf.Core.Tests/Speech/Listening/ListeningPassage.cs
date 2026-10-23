// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>A passage read aloud by the real voice.</summary>
/// <param name="Category">What kind of writing it is, such as prose or technical.</param>
/// <param name="Text">The passage.</param>
[DebuggerDisplay("ListeningPassage: {Category}")]
public sealed record ListeningPassage(string Category, string Text)
{
    /// <inheritdoc/>
    public override string ToString() => Category;
}
