// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>How HyperPdf decides one rule of a profile.</summary>
internal enum StandardsRuleKind
{
    /// <summary>HyperPdf has no check for the rule.</summary>
    NotChecked = 0,

    /// <summary>A person has to judge the rule.</summary>
    Human = 1,

    /// <summary>An accessibility report code or a Well-Tagged PDF finding decides the rule.</summary>
    Linked = 2,

    /// <summary>The reuse declaration of Well-Tagged PDF decides the rule.</summary>
    ReuseDeclaration = 3,

    /// <summary>The accessibility declaration of Well-Tagged PDF decides the rule.</summary>
    AccessibilityDeclaration = 4,

    /// <summary>A parent-child pair of structure types decides the rule.</summary>
    Pair = 5,
}
