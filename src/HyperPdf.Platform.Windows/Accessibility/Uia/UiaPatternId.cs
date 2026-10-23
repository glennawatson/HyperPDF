// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The UI Automation control patterns the bridge offers.</summary>
internal enum UiaPatternId
{
    /// <summary>The pattern for clicking.</summary>
    Invoke = 10_000,

    /// <summary>A text value.</summary>
    Value = 10_002,

    /// <summary>Opening and closing.</summary>
    ExpandCollapse = 10_005,

    /// <summary>Being one of several items to pick.</summary>
    SelectionItem = 10_010,

    /// <summary>Reading text by character, word and line.</summary>
    Text = 10_014,

    /// <summary>Switching on and off.</summary>
    Toggle = 10_015,
}
