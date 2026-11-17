using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions
{
    public class ConstantExpression<T> : Expression
    {
        T value;

        public ConstantExpression(T v)
        {
            value = v;
        }

        public override object? Evaluate(EvaluationContext feature)
        {
            return value;
        }

        public override object? PossibleOutputs()
        {
            return (T)new object();
        }

        public bool Equals(ConstantExpression<T>? other)
        {
            return other is not null && (ReferenceEquals(value, other.value) || Equals(value, other.value));
        }

        public override bool Equals(object? obj) => obj is ConstantExpression<T> other && Equals(other);

        public override int GetHashCode() => value?.GetHashCode() ?? 0;
    }
}
