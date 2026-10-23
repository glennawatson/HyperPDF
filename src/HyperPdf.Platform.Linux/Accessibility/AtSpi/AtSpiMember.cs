// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>One method or property of an AT-SPI interface, as the introspection data describes it.</summary>
/// <param name="Name">The member's name.</param>
/// <param name="Input">The D-Bus signature of the arguments, or empty for none; for a property it is empty.</param>
/// <param name="Output">The D-Bus signature of the result; for a property it is the type of the value.</param>
/// <param name="IsProperty">Whether the member is a read only property rather than a method.</param>
[DebuggerDisplay("AtSpiMember: {Name} ({Input}) -> {Output}")]
internal readonly record struct AtSpiMember(string Name, string Input, string Output, bool IsProperty);
