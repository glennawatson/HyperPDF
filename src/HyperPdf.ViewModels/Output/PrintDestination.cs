// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>Where a print goes.</summary>
public enum PrintDestination
{
    /// <summary>Straight to a printer's queue with the settings chosen in the preview.</summary>
    Printer = 0,

    /// <summary>A PDF file, chosen in a save dialog.</summary>
    SaveAsPdf = 1,

    /// <summary>The desktop's own print dialog, for settings the preview does not offer.</summary>
    SystemDialog = 2,
}
