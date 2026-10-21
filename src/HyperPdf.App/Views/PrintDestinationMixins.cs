// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Views;

/// <summary>Words for print destinations.</summary>
internal static class PrintDestinationMixins
{
    /// <summary>Adds the main button text of a destination.</summary>
    /// <param name="destination">The destination.</param>
    extension(PrintDestination destination)
    {
        /// <summary>Gets the main button's text: Print sends at once, the others open a dialog next.</summary>
        internal string ButtonText => destination switch
        {
            PrintDestination.Printer => "Print",
            PrintDestination.SaveAsPdf => "Save…",
            _ => "Print…",
        };
    }
}
