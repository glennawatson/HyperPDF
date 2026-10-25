// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A style with everything it inherits settled.</summary>
/// <param name="Paragraph">The paragraph formatting.</param>
/// <param name="Character">The character formatting.</param>
[DebuggerDisplay("ResolvedStyle")]
internal sealed record ResolvedStyle(ResolvedParagraph Paragraph, ResolvedCharacter Character);
