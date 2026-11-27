using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;

namespace VecTiles.Styles.MapLibre.Expressions;

/// <summary>
/// Class holding StoppedFloat data in Json format
/// </summary>
public class StoppedFloat : IStopped<float>
{
    bool _hasDefault = false;
    float _default;

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public float Default
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
    /// Expression to evaluate, e.g. ["interpolate", ["linear"], ["zoom"], ...]
    /// or ["get", "property"]. Data-driven form per Mapbox Style Spec. If set,
    /// it takes precedence over stops, single value and default.
    /// </summary>
    public IExpression? Expression { get; set; }

    bool _hasSingleVal = false;
    float _singleVal;

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public float SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, float>> _stops = [];

    /// <summary>The interpolation base of the exponential stop interpolation.</summary>
    public float Base { get; set; } = 1f;

    /// <summary>The zoom/value stops of the stopped function, in ascending zoom order.</summary>
    public IList<KeyValuePair<float, float>> Stops
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
    public float Evaluate(EvaluationContext ctx)
    {
        if (Expression != null)
        {
            return StoppedExpression.ToFloat(Expression.Evaluate(ctx));
        }

        return Evaluate(ctx, StopsType.Exponential);
    }

    /// <summary>
    /// Calculate the correct value for a stopped function
    /// No Bezier type up to now
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <param name="stoppsType">Type of calculation (interpolate, exponential, categorical)</param>
    /// <returns>Value for this stopp respecting zoom factor and type</returns>
    public float Evaluate(EvaluationContext ctx, StopsType stoppsType)
    {
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
                        case StopsType.Exponential:
                            var progress = zoom - lastZoom;
                            var difference = nextZoom - lastZoom;
                            if (difference < float.Epsilon)
                                return 0;
                            if (Base - 1.0f < float.Epsilon)
                                return lastValue + (nextValue - lastValue) * progress / difference;
                            else
                            {
                                //var r = FromResolution(resolution);
                                //var lr = FromResolution(lastResolution);
                                //var nr = FromResolution(nextResolution);
                                //var logBase = Math.Log(Base);
                                //return lastValue + (float)((nextValue - lastValue) * (Math.Pow(Base, lr-r) - 1) / (Math.Pow(Base, lr-nr) - 1));
                                //return lastValue + (float)((nextValue - lastValue) * (Math.Exp(progress * logBase) - 1) / (Math.Exp(difference * logBase) - 1)); // (Math.Pow(Base, progress) - 1) / (Math.Pow(Base, difference) - 1));
                                return lastValue + (float)((nextValue - lastValue) * (Math.Pow(Base, progress) - 1) / (Math.Pow(Base, difference) - 1));
                            }
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

        return 0;
    }

    /// <inheritdoc />
    public float PossibleOutputs()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Expression != null)
        {
            return "StoppedFloat [Expression]";
        }

        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedFloat [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedFloat [SingleVal={SingleVal}]";
        }

        return $"StoppedFloat [Default={Default}]";
    }
}
