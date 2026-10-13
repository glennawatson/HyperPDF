// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A paragraph of rich text: runs that wrap together and share an alignment.</summary>
/// <param name="Runs">The runs.</param>
/// <param name="Alignment">The alignment: 0 left, 1 centred, 2 right, or <see cref="RichStyle.NoAlignment"/> to use the field's.</param>
[DebuggerDisplay("RichParagraph: {Runs.Count} runs")]
internal sealed record RichParagraph(List<RichRun> Runs, int Alignment);
