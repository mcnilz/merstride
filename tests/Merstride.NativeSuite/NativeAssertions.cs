using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Merstride.NativeTesting;

// Only the explicit constraints used by the shared scenario worlds are supported.
// NUnit remains the managed host. The native driver cannot use NUnit's runtime
// reflection/code generation, and must never turn an unsupported assertion into success.
internal sealed class AssertionException(string message) : Exception(message);

internal static class Assert
{
    internal static void That<T>(T actual, Constraint constraint, string? message = null)
    {
        if (!constraint.Matches(actual)) throw new AssertionException($"{message} Expected {constraint.Description}; actual: {actual}");
    }
    internal static void Fail(string message) => throw new AssertionException(message);
}

internal static class TestContext
{
    internal static TextWriter Out => Console.Out;
}

internal sealed class Constraint(Func<object?, bool> predicate, string description)
{
    internal string Description => description;
    internal bool Matches(object? value) => predicate(value);
    internal ConstraintBuilder And => new(this);
    internal Constraint IgnoreCase => CanIgnoreCase
        ? new(value => value is string text && string.Equals(text, ExpectedText, StringComparison.OrdinalIgnoreCase), description + " ignoring case")
        : throw new AssertionException("Native IgnoreCase supports only a single positive string equality constraint.");
    internal string? ExpectedText { get; init; }
    internal bool CanIgnoreCase { get; init; }
}

internal sealed class ConstraintBuilder(Constraint? prior = null, bool negate = false, bool count = false)
{
    internal ConstraintBuilder Not => new(prior, !negate, count);
    internal Constraint EqualTo(object? expected) => Build(value => Equal(value, expected), $"equal to {expected}", expected as string);
    internal Constraint EquivalentTo(IEnumerable expected) => Build(value => value is IEnumerable actual && Equivalent(actual, expected), "equivalent collection");
    internal Constraint Contain(string expected) => Build(value => value is string text && text.Contains(expected, StringComparison.Ordinal), $"containing {expected}");
    internal Constraint Contain(char expected) => Contain(expected.ToString());
    internal Constraint StartWith(string expected) => Build(value => value is string text && text.StartsWith(expected, StringComparison.Ordinal), $"starting with {expected}");
    internal Constraint Match(string expected) => Build(value => value is string text && Regex.IsMatch(text, expected, RegexOptions.CultureInvariant), $"matching {expected}");
    internal Constraint AnyOf(params object[] expected) => Build(value => expected.Any(item => Equal(value, item)), "one of the allowed values");
    internal Constraint InRange(object minimum, object maximum) => Build(value => Compare(value, minimum) >= 0 && Compare(value, maximum) <= 0, "inside range");
    internal Constraint LessThan(object expected) => Build(value => Compare(value, expected) < 0, $"less than {expected}");
    internal Constraint LessThanOrEqualTo(object expected) => Build(value => Compare(value, expected) <= 0, $"at most {expected}");
    internal Constraint GreaterThan(object expected) => Build(value => Compare(value, expected) > 0, $"greater than {expected}");
    internal Constraint GreaterThanOrEqualTo(object expected) => Build(value => Compare(value, expected) >= 0, $"at least {expected}");
    internal Constraint Zero => EqualTo(0);
    internal Constraint Positive => GreaterThan(0);
    internal Constraint True => EqualTo(true);
    internal Constraint False => EqualTo(false);
    internal Constraint Null => EqualTo(null);
    internal Constraint Empty => Build(value => value is string text ? text.Length == 0 : value is IEnumerable sequence && !sequence.Cast<object?>().Any(), "empty");

    private Constraint Build(Func<object?, bool> test, string description, string? expectedText = null) =>
        new(value => (prior?.Matches(value) ?? true) && (negate != test(count ? Count(value) : value)), description)
            { ExpectedText = expectedText, CanIgnoreCase = expectedText is not null && prior is null && !negate && !count };

    private static int Count(object? value) => value switch
    {
        string text => text.Length,
        IEnumerable sequence => sequence.Cast<object?>().Count(),
        _ => throw new AssertionException("Unsupported native Count/Length assertion target.")
    };

    private static int Compare(object? actual, object expected) => actual is TimeSpan elapsed && expected is TimeSpan deadline
        ? elapsed.CompareTo(deadline)
        : actual is not null && Numeric(actual) && Numeric(expected)
            ? Convert.ToDecimal(actual, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(expected, CultureInfo.InvariantCulture))
            : throw new AssertionException("Unsupported native comparison assertion target.");

    private static bool Numeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool Equal(object? actual, object? expected)
    {
        if (actual is null || expected is null) return actual is null && expected is null;
        if (Numeric(actual) && Numeric(expected)) return Compare(actual, expected) == 0;
        if (actual is string || expected is string) return actual.Equals(expected);
        if (actual is IEnumerable left && expected is IEnumerable right)
        {
            var a = left.Cast<object?>().ToArray(); var b = right.Cast<object?>().ToArray();
            return a.Length == b.Length && a.Zip(b).All(pair => Equal(pair.First, pair.Second));
        }
        return actual.Equals(expected);
    }

    private static bool Equivalent(IEnumerable actual, IEnumerable expected)
    {
        var remaining = expected.Cast<object?>().ToList();
        foreach (var value in actual)
        {
            var index = remaining.FindIndex(item => Equal(value, item));
            if (index < 0) return false;
            remaining.RemoveAt(index);
        }
        return remaining.Count == 0;
    }
}

internal static class Is
{
    internal static ConstraintBuilder Not => new(negate: true);
    internal static Constraint EqualTo(object? expected) => new ConstraintBuilder().EqualTo(expected);
    internal static Constraint EquivalentTo(IEnumerable expected) => new ConstraintBuilder().EquivalentTo(expected);
    internal static Constraint AnyOf(params object[] expected) => new ConstraintBuilder().AnyOf(expected);
    internal static Constraint InRange(object minimum, object maximum) => new ConstraintBuilder().InRange(minimum, maximum);
    internal static Constraint LessThan(object expected) => new ConstraintBuilder().LessThan(expected);
    internal static Constraint LessThanOrEqualTo(object expected) => new ConstraintBuilder().LessThanOrEqualTo(expected);
    internal static Constraint GreaterThan(object expected) => new ConstraintBuilder().GreaterThan(expected);
    internal static Constraint GreaterThanOrEqualTo(object expected) => new ConstraintBuilder().GreaterThanOrEqualTo(expected);
    internal static Constraint Zero => EqualTo(0);
    internal static Constraint Positive => GreaterThan(0);
    internal static Constraint True => EqualTo(true);
    internal static Constraint False => EqualTo(false);
    internal static Constraint Empty => new ConstraintBuilder().Empty;
}

internal static class Does
{
    internal static ConstraintBuilder Not => new(negate: true);
    internal static Constraint Contain(string expected) => new ConstraintBuilder().Contain(expected);
    internal static Constraint Contain(char expected) => new ConstraintBuilder().Contain(expected);
    internal static Constraint StartWith(string expected) => new ConstraintBuilder().StartWith(expected);
    internal static Constraint Match(string expected) => new ConstraintBuilder().Match(expected);
}

internal static class Has
{
    internal static ConstraintBuilder Count => new(count: true);
    internal static ConstraintBuilder Length => new(count: true);
}
