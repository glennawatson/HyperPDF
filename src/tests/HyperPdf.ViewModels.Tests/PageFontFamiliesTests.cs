// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the screen font cache keeps whatever font type the render head uses.</summary>
public sealed class PageFontFamiliesTests
{
    /// <summary>A family is found only after it was added, whatever the case of its name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedFamilyIsFoundIgnoringCase()
    {
        var families = new PageFontFamilies();
        var font = new object();

        var before = families.TryGet("Helvetica", out _);
        families.Add("Helvetica", font);
        var after = families.TryGet("HELVETICA", out var found);

        await Assert.That(before).IsFalse();
        await Assert.That(after).IsTrue();
        await Assert.That(found).IsSameReferenceAs(font);
    }

    /// <summary>A name that was never added is not found.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFamilyIsNotFound()
    {
        var families = new PageFontFamilies();
        object other = new();
        families.Add("Times", other);

        var found = families.TryGet("Courier", out var font);

        await Assert.That(found).IsFalse();
        await Assert.That(font).IsNull();
    }
}
