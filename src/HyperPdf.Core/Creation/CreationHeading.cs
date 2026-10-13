// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>A heading. A heading is at most one level below the heading before it, and the first is level 1.</summary>
/// <param name="Level">The level, 1 to 6.</param>
/// <param name="Text">The heading's text.</param>
[DebuggerDisplay("CreationHeading: H{Level} {Text}")]
public sealed record CreationHeading(int Level, string Text) : CreationBlock;
