using VecTiles.Common.Primitives;

namespace VecTiles.Styles.Mapbox.Expressions;

/// <summary>
/// Class holding StoppedColor data
/// </summary>
public class StoppedColor : IStopped<Color>
{
    bool _hasDefault = false;
    Color _default = Color.Empty;

    /// <summary>
    /// Default value to use, if nothing else is set
    /// </summary>
    public Color Default
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
    public bool HasOnlyDefault => _hasDefault & !(_hasSingleVal | _hasStops);

    /// <summary>
    /// True, if value doesn't change with context
    /// </summary>
    public bool IsConstant => !_hasStops;

    bool _hasSingleVal = false;
    Color _singleVal = Color.Empty;

    /// <summary>
    /// SingleVal to use, if there are no stops
    /// </summary>
    public Color SingleVal
    {
        get => _singleVal;
        set
        {
            _hasSingleVal = true;
            _singleVal = value;
        }
    }

    bool _hasStops = false;
    IList<KeyValuePair<float, Color>> _stops = [];

    public float Base { get; set; } = 1f;

    public IList<KeyValuePair<float, Color>> Stops
    {
        get => _stops;
        set
        {
            _hasStops = true;
            _stops = value;
        }
    }

    /// <summary>
    /// Calculate the correct color for a stopped function
    /// No Bezier type up to now
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <returns>Value for this stopp respecting zoom factor</returns>
    public Color Evaluate(EvaluationContext ctx)
    {
        return Evaluate(ctx, StopsType.Exponential);
    }

    /// <summary>
    /// Calculate the correct color for a stopped function
    /// No Bezier type up to now
    /// </summary>
    /// <param name="contextZoom">Zoom factor for calculation </param>
    /// <param name="stoppsType">Type of calculation (interpolate, exponential, categorical)</param>
    /// <returns>Value for this stopp respecting zoom factor and type</returns>
    public Color Evaluate(EvaluationContext ctx, StopsType stoppsType)
    {
        if (_hasStops)
        {
            float zoom = ctx.Zoom ?? 0f;

            var lastZoom = Stops[0].Key;
            var lastColor = Stops[0].Value;

            if (lastZoom > zoom)
                return lastColor;

            for (int i = 1; i < Stops.Count; i++)
            {
                var nextZoom = Stops[i].Key;
                var nextColor = Stops[i].Value;

                if (zoom == nextZoom)
                    return nextColor;

                if (lastZoom <= zoom && zoom < nextZoom)
                {
                    switch (stoppsType)
                    {
                        case StopsType.Interval:
                            return lastColor;
                        case StopsType.Exponential:
                            var progress = zoom - lastZoom;
                            var difference = nextZoom - lastZoom;
                            if (difference < float.Epsilon)
                                return Color.Empty;
                            float factor;
                            if (Base - 1 < float.Epsilon)
                                factor = progress / difference;
                            else
                                factor = (float)((Math.Pow(Base, progress) - 1) / (Math.Pow(Base, difference) - 1));
                            var r = (byte)Math.Round(lastColor.R + (nextColor.R - lastColor.R) * factor);
                            var g = (byte)Math.Round(lastColor.G + (nextColor.G - lastColor.G) * factor);
                            var b = (byte)Math.Round(lastColor.B + (nextColor.B - lastColor.B) * factor);
                            var a = (byte)Math.Round(lastColor.A + (nextColor.A - lastColor.A) * factor);
                            return new Color(r, g, b, a);
                        case StopsType.Categorical:
                            // ==
                            if (nextZoom - zoom < float.Epsilon)
                                return nextColor;
                            break;
                    }
                }

                lastZoom = nextZoom;
                lastColor = nextColor;
            }

            return lastColor;
        }

        if (_hasSingleVal)
        {
            return SingleVal;
        }

        if (_hasDefault)
        {
            return Default;
        }

        return Color.Empty;
    }

    public Color PossibleOutputs()
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        if (_hasStops && _stops.Count > 0)
        {
            return "StoppedColor [Stops= " + string.Join(",", _stops.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
        }

        if (_hasSingleVal)
        {
            return $"StoppedColor [SingleVal={SingleVal}]";
        }

        return $"StoppedColor [Default={Default}]";
    }
}
