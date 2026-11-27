using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions
{
    /// <summary>
    /// An expression that always evaluates to a single constant value of
    /// type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the constant value.</typeparam>
    public class ConstantExpression<T> : Expression
    {
        T value;

        /// <summary>
        /// Initializes a new <see cref="ConstantExpression{T}"/> with the given value.
        /// </summary>
        /// <param name="v">The constant value of the expression.</param>
        public ConstantExpression(T v)
        {
            value = v;
        }

        /// <summary>
        /// Evaluates the expression - always returns the constant value.
        /// </summary>
        /// <param name="feature">The evaluation context (ignored).</param>
        /// <returns>Always returns the constant value.</returns>
        public override object? Evaluate(EvaluationContext feature)
        {
            return value;
        }

        /// <summary>
        /// Returns the possible outputs of this expression.
        /// </summary>
        /// <returns>The possible output(s) of the expression.</returns>
        public override object? PossibleOutputs()
        {
            return (T)new object();
        }

        /// <summary>
        /// Indicates whether the current object is equal to another
        /// <see cref="ConstantExpression{T}"/>, comparing the constant values.
        /// </summary>
        /// <param name="other">The constant expression to compare with.</param>
        /// <returns>True if both constant values are equal; otherwise, false.</returns>
        public bool Equals(ConstantExpression<T>? other)
        {
            return other is not null && (ReferenceEquals(value, other.value) || Equals(value, other.value));
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is ConstantExpression<T> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => value?.GetHashCode() ?? 0;
    }
}
