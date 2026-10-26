// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>A name and value shown in the properties dialog.</summary>
/// <param name="Name">The property name.</param>
/// <param name="Value">The property value.</param>
[DebuggerDisplay("PropertyEntry: {Name}: {Value}")]
public sealed record PropertyEntry(string Name, string Value)
{
    /// <summary>The retained interface label, when this is an application-defined property.</summary>
    private readonly LocalizedMessage? _nameText;

    /// <summary>The retained interface value, when its words belong to the application.</summary>
    private readonly LocalizedMessage? _valueText;

    /// <summary>Initializes a new instance of the <see cref="PropertyEntry"/> class with retained presentation identities.</summary>
    /// <param name="name">The property label.</param>
    /// <param name="value">Its literal or translated value.</param>
    public PropertyEntry(in LocalizedMessage name, in LocalizedMessage value)
        : this(name.ToString(), value.ToString())
    {
        _nameText = name;
        _valueText = value;
    }

    /// <summary>Gets the current translated property label.</summary>
    public string DisplayName => _nameText?.ToString() ?? Name;

    /// <summary>Gets the value without interpreting document text as a resource key.</summary>
    public string DisplayValue => _valueText?.ToString() ?? Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PropertyEntry? other) => other is not null && Name == other.Name && Value == other.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(Name, Value);
}
