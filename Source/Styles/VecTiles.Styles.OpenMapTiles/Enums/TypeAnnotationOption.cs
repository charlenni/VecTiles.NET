namespace VecTiles.Styles.OpenMapTiles.Enums;

/// <summary>
/// Controls the annotation behavior of the parser when encountering an expression
/// whose type is not a subtype of the expected type.The default behavior, used
/// when optional<TypeAnnotationOption> is a nullopt, is as follows:
/// When we expect a number, string, boolean, or array but have a value, wrap it in an assertion.
/// When we expect a color or formatted string, but have a string or value, wrap it in a coercion.
/// Otherwise, we do static type-checking.
/// These behaviors are overridable for:
/// * The "coalesce" operator, which needs to omit type annotations.
/// * String-valued properties (e.g. `text-field`), where coercion is more convenient than assertion.
/// </summary>
public enum TypeAnnotationOption
{
    /// <summary>
    /// Keeps the parser's default behavior, which asserts value expressions for
    /// number, string, boolean, and array expectations and coerces them for color
    /// or formatted-string expectations.
    /// </summary>
    None,
    /// <summary>
    /// Wraps a mismatched value expression in a coercion instead of an assertion.
    /// </summary>
    Coerce,
    /// <summary>
    /// Wraps a mismatched value expression in an assertion instead of a coercion.
    /// </summary>
    Assert,
    /// <summary>
    /// Omits type annotations entirely, e.g. for the "coalesce" operator.
    /// </summary>
    Omit,
}
