using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class FixedPointToolshedCompatibilityTest
{
    [TestCase("12", "12")]
    [TestCase("1.25", "1.25")]
    [TestCase("-2.50", "-2.50")]
    public async Task RegisteredParserReadsRetainedNumericType(string input, string expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Server.WaitAssertion(() =>
        {
            var toolshed = pair.Server.ResolveDependency<ToolshedManager>();
            var context = new ParserContext(input, toolshed);
            Assert.That(toolshed.TryParse<FixedPoint2>(context, out var value), Is.True);
            Assert.That(value, Is.EqualTo(FixedPoint2.New(expected)));
            Assert.That(context.Error, Is.Null);
        });
        await pair.CleanReturnAsync();
    }
}
