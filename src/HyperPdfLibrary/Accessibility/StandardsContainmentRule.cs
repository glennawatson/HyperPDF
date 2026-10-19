// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>A parent-child rule read from the text of a veraPDF tagged PDF rule.</summary>
/// <param name="Kind">What the rule says about the pair.</param>
/// <param name="Parent">The parent's type, spelled as the table spells it.</param>
/// <param name="Child">The child's type, spelled as the table spells it.</param>
/// <param name="Conditional">Whether the rule applies only to a parent used in a particular way (as a grouping or non-grouping element).</param>
internal readonly record struct StandardsContainmentRule(StandardsContainmentKind Kind, string Parent, string Child, bool Conditional);
