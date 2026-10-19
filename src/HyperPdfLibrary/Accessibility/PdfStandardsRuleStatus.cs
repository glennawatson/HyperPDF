// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>What HyperPdf can say about one rule of a profile for one document.</summary>
public enum PdfStandardsRuleStatus
{
    /// <summary>HyperPdf checks the rule and the document meets it.</summary>
    Passed = 0,

    /// <summary>HyperPdf checks the rule and the document breaks it.</summary>
    Failed = 1,

    /// <summary>The rule needs a person to judge it; HyperPdf does not decide it.</summary>
    NeedsHumanReview = 2,

    /// <summary>HyperPdf has no check for the rule, so it says nothing about it. This is never a pass.</summary>
    NotChecked = 3,
}
