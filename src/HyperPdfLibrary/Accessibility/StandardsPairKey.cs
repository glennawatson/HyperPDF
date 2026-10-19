// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>A parent type, a child type and what a rule says about them, as the key of one parent-child rule.</summary>
/// <param name="Kind">What the rule says about the pair.</param>
/// <param name="Parent">The parent's type, spelled as the table spells it.</param>
/// <param name="Child">The child's type, spelled as the table spells it.</param>
internal readonly record struct StandardsPairKey(StandardsContainmentKind Kind, string Parent, string Child);
