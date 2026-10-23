// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Platform.Linux.Accessibility.AtSpi;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>Checks that what the AT-SPI server describes is what it answers.</summary>
public sealed class AtSpiIntrospectionTests
{
    /// <summary>Verifies that each interface describes exactly the methods and properties it answers.</summary>
    /// <param name="interfaceName">The interface.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("org.a11y.atspi.Accessible")]
    [Arguments("org.a11y.atspi.Component")]
    [Arguments("org.a11y.atspi.Action")]
    [Arguments("org.a11y.atspi.Text")]
    [Arguments("org.a11y.atspi.Document")]
    [Arguments("org.a11y.atspi.Application")]
    public async Task DescribedMembersAreTheAnsweredMembers(string interfaceName)
    {
        var describedMethods = new HashSet<string>(StringComparer.Ordinal);
        var describedProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in AtSpiIntrospection.MembersOf(interfaceName))
        {
            _ = (member.IsProperty ? describedProperties : describedMethods).Add(member.Name);
        }

        await Assert.That(describedMethods.SetEquals(AtSpiInterfaces.GetMethodNames(interfaceName))).IsTrue();
        await Assert.That(describedProperties.SetEquals(AtSpiInterfaces.GetPropertyNames(interfaceName))).IsTrue();
    }

    /// <summary>Verifies that the XML of an interface lists a method with its arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DescribeWritesMethodsWithTheirArguments()
    {
        var xml = System.Text.Encoding.UTF8.GetString(AtSpiIntrospection.Describe("org.a11y.atspi.Text").Span);

        await Assert.That(xml).Contains("<method name=\"GetStringAtOffset\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"u\"/><arg direction=\"out\" type=\"s\"/>");
        await Assert.That(xml).Contains("<property name=\"CharacterCount\" type=\"i\" access=\"read\"/>");
    }
}
