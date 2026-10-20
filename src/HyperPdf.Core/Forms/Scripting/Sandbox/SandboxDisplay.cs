// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The <c>display</c> object of a form script: the constants that say where a field shows. A script cannot change them.</summary>
internal sealed class SandboxDisplay : SandboxObject
{
    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => name switch
    {
        "visible" => SandboxValue.Of((double)FormFieldDisplay.Visible),
        "hidden" => SandboxValue.Of((double)FormFieldDisplay.Hidden),
        "noPrint" => SandboxValue.Of((double)FormFieldDisplay.NoPrint),
        "noView" => SandboxValue.Of((double)FormFieldDisplay.NoView),
        _ => SandboxValue.Undefined,
    };
}
