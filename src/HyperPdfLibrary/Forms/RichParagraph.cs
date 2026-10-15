// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A paragraph of rich text: a range of the runs of the <see cref="RichText"/> it was read into.</summary>
/// <param name="FirstRun">The first run's place in the text.</param>
/// <param name="RunCount">The number of runs.</param>
/// <param name="Alignment">The alignment: 0 left, 1 centred, 2 right, or <see cref="RichStyle.NoAlignment"/> to use the field's.</param>
[DebuggerDisplay("RichParagraph: {RunCount} runs")]
internal readonly record struct RichParagraph(int FirstRun, int RunCount, int Alignment);
