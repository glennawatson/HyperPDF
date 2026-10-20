// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>What a loop body has asked the loop to do.</summary>
internal enum SandboxSignal
{
    /// <summary>Carry on.</summary>
    None = 0,

    /// <summary>Leave the loop.</summary>
    Break = 1,

    /// <summary>Go on to the next round.</summary>
    Continue = 2,
}
