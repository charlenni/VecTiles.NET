using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Registry for the compound expression operators.
/// </summary>
public static class CompoundExpressionRegistry
{
    private static readonly Dictionary<string, List<CompoundExpression.IOperator>> Operators = [];

    private static bool _initialized;

    private static readonly object InitializationLock = new();

    /// <summary>
    /// Registers an operator. Called by <see cref="Register"/>.
    /// </summary>
    internal static void Add(CompoundExpression.IOperator op)
    {
        if (!Operators.TryGetValue(op.Name, out var list))
        {
            list = [];
            Operators[op.Name] = list;
        }

        list.Add(op);
    }

    /// <summary>
    /// Finds an operator by name, initializing the registry on first use.
    /// </summary>
    internal static CompoundExpression.IOperator? Find(string name)
    {
        EnsureInitialized();

        return Operators.TryGetValue(name, out var list) ? list[0] : null;
    }

    /// <summary>
    /// Is there an operator with this name?
    /// </summary>
    public static bool Has(string name)
    {
        EnsureInitialized();

        return Operators.ContainsKey(name);
    }

    internal static void EnsureInitialized()
    {
        if (_initialized)
            return;

        // The registry is filled from parallel running evaluations as well
        lock (InitializationLock)
        {
            if (_initialized)
                return;

            CompoundOperators.Register();

            _initialized = true;
        }
    }
}
