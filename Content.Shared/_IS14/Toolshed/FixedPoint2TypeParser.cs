using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Console;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Toolshed;

// Wizards bd217732d1 parser, adapted to the retained Goob numeric type.
public sealed class FixedPoint2TypeParser : TypeParser<FixedPoint2>
{
    public override bool TryParse(ParserContext ctx, out FixedPoint2 result)
    {
        var checkpoint = ctx.Save();
        if (Toolshed.TryParse(ctx, out int? value))
        {
            result = FixedPoint2.New(value.Value);
            return true;
        }

        ctx.Restore(checkpoint);
        ctx.Error = null;
        if (Toolshed.TryParse(ctx, out float? floatValue))
        {
            result = FixedPoint2.New(floatValue.Value);
            return true;
        }

        DebugTools.AssertNotNull(ctx.Error);
        result = FixedPoint2.Zero;
        return false;
    }

    public override CompletionResult? TryAutocomplete(ParserContext parserContext, CommandArgument? arg)
    {
        return CompletionResult.FromHint(GetArgHint(arg));
    }
}
