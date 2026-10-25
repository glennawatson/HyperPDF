// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The width a tab takes on one line.</summary>
/// <param name="AtomIndex">The tab's place among the paragraph's atoms.</param>
/// <param name="Width">The width in points the tab skips.</param>
/// <param name="Leader">What fills the width.</param>
[DebuggerDisplay("TabGap: atom {AtomIndex}, {Width} pt, {Leader}")]
internal readonly record struct TabGap(int AtomIndex, float Width, PdfTabLeader Leader);
