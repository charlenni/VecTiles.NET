using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.OpenMapTiles.Expressions;

/// <summary>
/// Class holding StoppedFloat array data
/// </summary>
public class StoppedFloatArray : IStopped<float[]>
{
    bool _hasDefault = false;
    float[] _default = [];

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public float[] Default
    {
        get => _default;
        set
        {
            _hasDefault = true;
            _default = value;
        }
    }

    /// <summary>
    /// Is this a stopped value that only contains the default
    /// </summary>
    public bool HasOnlyDefault => _hasDefault & !(_hasSingleVal | _hasStops) && Expression == null;

    /// <summary>
    /// True, if value doesn't change with context
    /// </summary>
    public bool IsConstant => !_hasStops && Expression == null;

    /// <summary>
    /// Expression to evaluate, e.g. ["literal", [0, 0]] or ["get", "property"].
    /// Data-driven form per Mapbox Style Spec. If set, it takes precedence
    /// over stops, single value and default.
    /// </summary>
    public IExpression? Expression { get; set; }

    bool _hasSingleVal = false;
    float[] _singleVal = [];

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public float[] SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, float[]>> _stops = [];

    /// <summary>The interpolation base of the exponential stop interpolation.</summary>
    public float Base { get; set; } = 1f;

    /// <summary>The zoom/value stops of the stopped function, in ascending zoom order.</summary>
    public IList<KeyValuePair<float, float[]>> Stops
    {
        get => _stops;
        set
        {
            _hasStops = true;
            _stops = value;
        }
    }

    /// <summary>
    /// Calculate the correct value for a stopped function
    /// No Bezier type up to now
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <returns>Value for this stopp respecting zoom factor</returns>
    public float[] Evaluate(EvaluationContext ctx)
    {
        return Evaluate(ctx, StopsType.Exponential);
    }

    /// <summary>
    /// Calculate the correct value for a stopped function
    /// No Bezier type up to now
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <param name="stoppsType">Type of calculation (interpolate, exponential, categorical)</param>
    /// <returns>Value for this stopp respecting zoom factor and type</returns>
    public float[] Evaluate(EvaluationContext ctx, StopsType stoppsType)
    {
        if (Expression != null)
        {
            var result = Expression.Evaluate(ctx);
            return StoppedExpression.ToFloatArray(result);
        }

        if (_hasStops)
        {
            float zoom = ctx.Zoom ?? 0f;

            var lastZoom = Stops[0].Key;
            var lastValue = Stops[0].Value;

            if (lastZoom > zoom)
                return lastValue;

            for (int i = 1; i < Stops.Count; i++)
            {
                var nextZoom = Stops[i].Key;
                var nextValue = Stops[i].Value;

                if (zoom == nextZoom)
                    return nextValue;

                if (lastZoom <= zoom && zoom < nextZoom)
                {
                    switch (stoppsType)
                    {
                        case StopsType.Interval:
                            return lastValue;
                        case StopsType.Categorical:
                            if (nextZoom - zoom < float.Epsilon)
                                return nextValue;
                            break;
                    }
                }

                lastZoom = nextZoom;
                lastValue = nextValue;
            }

            return lastValue;
        }

        if (_hasSingleVal)
        {
            return SingleVal;
        }

        if (_hasDefault)
        {
            return Default;
        }

        return [];
    }

    /// <inheritdoc />
    public float[] PossibleOutputs()
    {
        throw new System.NotImplementedException();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Expression != null)
        {
            return $"StoppedFloatArray [Expression={Expression}]";
        }

        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedFloatArray [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedFloatArray [SingleVal=[{string.Join(";", SingleVal.Select(v => $"{v}"))}]";
        }

        return $"StoppedFloatArray [Default=[{string.Join(";", Default.Select(v => $"{v}"))}]";
    }
}
