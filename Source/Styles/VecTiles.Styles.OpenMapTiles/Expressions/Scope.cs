using VecTiles.Common.Interfaces;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Variable scope for "let"/"var" expression handling. A scope holds the
/// bindings of a "let" expression and links to its parent scope; lookups
/// walk up the chain until a matching binding is found.
/// </summary>
public class Scope
{
    /// <summary>The variable bindings of this scope.</summary>
    public Dictionary<string, IExpression> Bindings { get; }
    /// <summary>The parent scope to fall back to, or null for the root scope.</summary>
    public Scope Parent { get; }

    /// <summary>
    /// Initializes a new <see cref="Scope"/> with the given bindings and parent.
    /// </summary>
    /// <param name="bindings">The variable bindings of this scope.</param>
    /// <param name="parent">The parent scope to fall back to, or null for the root scope.</param>
    public Scope(Dictionary<string, IExpression> bindings, Scope parent = null)
    {
        Bindings = bindings;
        Parent = parent;
    }

    /// <summary>
    /// Looks up the binding of a variable, walking up the parent scope chain.
    /// </summary>
    /// <param name="name">The name of the variable.</param>
    /// <returns>The expression bound to the name, or null if no binding exists.</returns>
    public IExpression GetBinding(string name)
    {
        if (Bindings.ContainsKey(name))
        {
            return Bindings[name];
        }
        else
        {
            if (Parent != null)
            {
                return Parent.GetBinding(name);
            }
        }

        return null;
    }
}
