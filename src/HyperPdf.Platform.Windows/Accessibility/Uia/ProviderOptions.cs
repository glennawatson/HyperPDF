// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>How UI Automation treats a provider.</summary>
[Flags]
internal enum ProviderOptions
{
    /// <summary>No options are set.</summary>
    None = 0,

    /// <summary>The provider runs in the client's process.</summary>
    ClientSideProvider = 1 << 0,

    /// <summary>The provider runs in the application's process.</summary>
    ServerSideProvider = 1 << 1,

    /// <summary>The provider is for the non client area of a window.</summary>
    NonClientAreaProvider = 1 << 2,

    /// <summary>The provider overrides the one UI Automation makes for a window.</summary>
    OverrideProvider = 1 << 3,

    /// <summary>The provider moves the focus itself.</summary>
    ProviderOwnsSetFocus = 1 << 4,

    /// <summary>Calls are made on the thread that created the provider.</summary>
    UseComThreading = 1 << 5,
}
