using VecTiles.Common.Enums;
using System.Globalization;
using System.Xml.Linq;
using VecTiles.Common.Interfaces;
using VecTiles.Common.Primitives;
using VecTiles.DataSources;
using VecTiles.Styles.Mapsforge.Enums;
using VecTiles.Styles.Mapsforge.Expressions;

namespace VecTiles.Styles.Mapsforge;

/// <summary>
/// Loads a Mapsforge render theme (XML file) into an <see cref="MFRenderTheme"/>
/// with a flat list of <see cref="MFLayerStyle"/> layers: one layer per drawing
/// instruction (line, area, circle, symbol, caption, lineSymbol, pathText), in
/// document order, with the conditions of its rule chain combined into a filter
/// expression.
/// </summary>
public static class MFRenderThemeLoader
{
    /// <summary>
    /// Highest zoom level a mapsforge rule without zoom-max applies to
    /// (attribute type of zoom-min/zoom-max is byte).
    /// </summary>
    private const int MaxRuleZoom = byte.MaxValue;

    /// <summary>
    /// Loads and parses a Mapsforge render theme from a file path or URL.
    /// </summary>
    /// <param name="source">File path or URL of the render theme. Plain file system paths are loaded through the file scheme of the ByteDataSource.</param>
    /// <param name="sourceName">Name of the data source the theme belongs to; defaults to "mapsforge".</param>
    /// <returns>The loaded render theme with its layers.</returns>
    /// <exception cref="ArgumentException">Thrown if the source cannot be read or the theme isn't valid.</exception>
    public static async Task<MFRenderTheme> Load(string source, string? sourceName = null)
    {
        // Plain file paths are normalized to the URL notation of the byte data source
        var themeSource = source.Contains(':') ? source : ToFileSource(source);

        var data = await ByteDataSource.GetBytesAsync(themeSource).ConfigureAwait(false);

        using var stream = new MemoryStream(data, writable: false);
        var document = XDocument.Load(stream);

        var root = document.Root;

        if (root == null || root.Name.LocalName != "rendertheme")
        {
            throw new ArgumentException($"'{source}' is not a Mapsforge render theme");
        }

        var namespaceName = root.Name.NamespaceName;

        var theme = new MFRenderTheme
        {
            Name = sourceName ?? "mapsforge",
            Source = sourceName ?? "mapsforge",
            Version = ParseInt(root.Attribute("version")?.Value) ?? 6,
            MapBackground = ParseColor(root.Attribute("map-background")?.Value) ?? Colors.White,
            MapBackgroundOutside = ParseColor(root.Attribute("map-background-outside")?.Value) ?? Colors.White,
            BaseStrokeWidth = ParseFloat(root.Attribute("base-stroke-width")?.Value) ?? 1f,
            BaseTextSize = ParseFloat(root.Attribute("base-text-size")?.Value) ?? 1f
        };

        // Base location icon and pattern sources are resolved against (folder
        // of the theme file in file URL notation, or null)
        MFSpriteFactory spriteFactory = new(GetBaseDirectory(themeSource));

        // Populate the layers with the rules of the theme, in document order
        var context = new RuleContext
        {
            ZoomMin = 0,
            ZoomMax = MaxRuleZoom,
            Cat = string.Empty,
            Conditions = [],
            Theme = theme,
            SpriteFactory = spriteFactory
        };

        foreach (var child in root.Elements())
        {
            if (child.Name.LocalName == "stylemenu")
            {
                theme.StyleMenu = ParseStyleMenu(child, namespaceName);
            }
            else if (child.Name.LocalName == "rule")
            {
                ParseRule(child, context);
            }
            // Other root elements (hillshading) are not supported yet
        }

        return theme;
    }

    /// <summary>
    /// Gets the base directory of a theme file in ByteDataSource URL notation, resolved from
    /// the URL or path of the theme file; null for sources without file system location. Icon
    /// and pattern sources of a theme are resolved against this directory, so a renderer built
    /// from the loaded theme creates its sprite factory with the same base directory.
    /// </summary>
    /// <param name="source">File path or URL of the render theme, as passed to <see cref="Load"/>.</param>
    /// <returns>The directory containing the theme file, without trailing slash; null when the
    /// theme file is not a file system path or file URL.</returns>
    public static string? GetThemeBaseDirectory(string source)
    {
        // Plain file paths are normalized to the URL notation of the byte data source
        var themeSource = source.Contains(':') ? source : ToFileSource(source);
        return GetBaseDirectory(themeSource);
    }

    /// <summary>
    /// Gets the base directory of the theme in ByteDataSource URL notation,
    /// resolved from the URL of the theme file; null for other sources.
    /// </summary>
    private static string? GetBaseDirectory(string themeSource)
    {
        if (!themeSource.StartsWith(ByteDataSource.FileScheme + "://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = themeSource[(ByteDataSource.FileScheme.Length + 3)..];
        var lastSeparator = path.LastIndexOf('/');

        return lastSeparator < 0 ? string.Empty : path[..lastSeparator];
    }

    /// <summary>
    /// Converts a plain file system path to the file URL notation of the
    /// ByteDataSource ("file://" on Unix-like systems, "file:///" on Windows).
    /// </summary>
    private static string ToFileSource(string source)
    {
        var fullPath = Path.GetFullPath(source);

        return Path.DirectorySeparatorChar == '/'
            ? $"file://{fullPath}"
            : $"file:///{fullPath}";
    }

    /// <summary>
    /// Parses the stylemenu element of the theme: menu layers with their names,
    /// categories and overlays.
    /// </summary>
    private static MFStyleMenu ParseStyleMenu(XElement element, string namespaceName)
    {
        var menu = new MFStyleMenu
        {
            Id = element.Attribute("id")?.Value ?? string.Empty,
            DefaultValue = element.Attribute("defaultvalue")?.Value ?? string.Empty,
            DefaultLanguage = element.Attribute("defaultlang")?.Value ?? string.Empty
        };

        foreach (var layerElement in element.Elements().Where(e => e.Name.LocalName == "layer"))
        {
            var layer = new MFMenuLayer
            {
                Id = layerElement.Attribute("id")?.Value ?? string.Empty,
                Parent = layerElement.Attribute("parent")?.Value,
                Visible = ParseBool(layerElement.Attribute("visible")?.Value) ?? true,
                Enabled = ParseBool(layerElement.Attribute("enabled")?.Value) ?? true
            };

            foreach (var nameElement in layerElement.Elements().Where(e => e.Name.LocalName == "name"))
            {
                layer.Names[nameElement.Attribute("lang")?.Value ?? ""] = nameElement.Attribute("value")?.Value ?? string.Empty;
            }

            foreach (var catElement in layerElement.Elements().Where(e => e.Name.LocalName == "cat"))
            {
                if (catElement.Attribute("id")?.Value is string cat)
                {
                    layer.Categories.Add(cat);
                }
            }

            foreach (var overlayElement in layerElement.Elements().Where(e => e.Name.LocalName == "overlay"))
            {
                if (overlayElement.Attribute("id")?.Value is string overlay)
                {
                    layer.Overlays.Add(overlay);
                }
            }

            menu.Layers.Add(layer);
        }

        return menu;
    }

    /// <summary>
    /// Parses a rule element recursively: the conditions of the rule (element
    /// type, closed, tags, zoom, category) are added to the condition chain for
    /// its child rules and drawing instructions.
    /// </summary>
    /// <param name="parentSymbols">Symbol layers with an id of the enclosing
    /// rules; captions may reference them via the "symbol-id" attribute.</param>
    private static void ParseRule(XElement element, RuleContext parentContext,
        IReadOnlyDictionary<string, MFSymbolLayerStyle>? parentSymbols = null)
    {
        var zoomMin = parentContext.ZoomMin;
        var zoomMax = parentContext.ZoomMax;

        var ruleZoomMin = ParseInt(element.Attribute("zoom-min")?.Value);
        var ruleZoomMax = ParseInt(element.Attribute("zoom-max")?.Value);

        if (ruleZoomMin.HasValue)
        {
            zoomMin = Math.Max(parentContext.ZoomMin, ruleZoomMin.Value);
        }

        if (ruleZoomMax.HasValue)
        {
            zoomMax = Math.Min(parentContext.ZoomMax, ruleZoomMax.Value);
        }

        var cat = element.Attribute("cat")?.Value ?? parentContext.Cat;

        var conditions = parentContext.Conditions;

        // Conditions of a rule only narrow its parent context, so as long as
        // the rule does not add its own condition, the parent is reused as is
        var elementCondition = ParseElementCondition(element);
        var closedCondition = ParseClosedCondition(element);
        var tagCondition = ParseTagCondition(element);

        if (elementCondition != null || closedCondition != null || tagCondition != null)
        {
            conditions = [.. conditions];

            if (elementCondition != null)
            {
                conditions.Add(elementCondition);
            }

            if (closedCondition != null)
            {
                conditions.Add(closedCondition);
            }

            if (tagCondition != null)
            {
                conditions.Add(tagCondition);
            }
        }

        var context = new RuleContext
        {
            ZoomMin = zoomMin,
            ZoomMax = zoomMax,
            Cat = cat,
            Conditions = conditions,
            Theme = parentContext.Theme,
            SpriteFactory = parentContext.SpriteFactory
        };

        // Symbol layers with an id are collected first (including those of the
        // enclosing rules), so captions of the same or a nested rule can
        // resolve their "symbol-id" reference to it
        var symbolLayers = new Dictionary<string, MFSymbolLayerStyle>();

        if (parentSymbols != null)
        {
            foreach (var symbolId in parentSymbols)
            {
                symbolLayers[symbolId.Key] = symbolId.Value;
            }
        }

        var children = element.Elements().ToList();

        foreach (var child in children)
        {
            switch (child.Name.LocalName)
            {
                case "rule":
                    ParseRule(child, context, symbolLayers);
                    break;

                case "symbol":
                    var symbolLayer = ParseSymbolLayer(child, context, LayerNameSuffix(context));
                    context.Theme.Layers.Add(symbolLayer);

                    var symbolId = child.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(symbolId))
                    {
                        symbolLayers[symbolId] = symbolLayer;
                    }

                    break;

                case "caption":
                    context.Theme.Layers.Add(ParseCaptionLayer(child, context, LayerNameSuffix(context), symbolLayers));
                    break;

                case "circle":
                    context.Theme.Layers.Add(ParseCircleLayer(child, context, LayerNameSuffix(context)));
                    break;

                case "line":
                    context.Theme.Layers.Add(ParseLineLayer(child, context, LayerNameSuffix(context)));
                    break;

                case "area":
                    context.Theme.Layers.Add(ParseAreaLayer(child, context, LayerNameSuffix(context)));
                    break;

                case "lineSymbol":
                    context.Theme.Layers.Add(ParseLineSymbolLayer(child, context, LayerNameSuffix(context)));
                    break;

                case "pathText":
                    context.Theme.Layers.Add(ParsePathTextLayer(child, context, LayerNameSuffix(context)));
                    break;

                // Unknown elements (hillshading) are skipped
            }
        }
    }

    /// <summary>
    /// Parses the element condition (attribute e) of a rule, or null for the
    /// default "any".
    /// </summary>
    private static IExpression? ParseElementCondition(XElement element)
    {
        var value = element.Attribute("e")?.Value;

        var parsed = value?.ToLowerInvariant() switch
        {
            "node" => MapsforgeElement.Node,
            "way" => MapsforgeElement.Way,
            "any" or null or "" => MapsforgeElement.Any,
            _ => throw new ArgumentException($"Unknown element type '{value}' in rule condition")
        };

        return parsed == MapsforgeElement.Any ? null : new MFElementExpression(parsed);
    }

    /// <summary>
    /// Parses the closed condition (attribute closed) of a rule, or null for
    /// the default "any".
    /// </summary>
    private static IExpression? ParseClosedCondition(XElement element)
    {
        var value = element.Attribute("closed")?.Value;

        var parsed = value?.ToLowerInvariant() switch
        {
            "yes" => MapsforgeClosed.Yes,
            "no" => MapsforgeClosed.No,
            "any" or null or "" => MapsforgeClosed.Any,
            _ => MapsforgeClosed.Any
        };

        return parsed == MapsforgeClosed.Any ? null : new MFClosedExpression(parsed);
    }

    /// <summary>
    /// Parses the tag condition (attributes k and v) of a rule, or null for
    /// the default "any" key and value.
    /// </summary>
    private static IExpression? ParseTagCondition(XElement element)
    {
        var k = element.Attribute("k")?.Value;
        var v = element.Attribute("v")?.Value;

        var anyKey = k is null or "*" or "";
        var anyValue = v is null or "*" or "";

        if (anyKey && anyValue)
        {
            return null;
        }

        // The v attribute may remove tokens from the match: a token "~" makes
        // the rule match when no tag of the keys is present, or when a tag
        // with one of the remaining values exists (negation); a token "-"
        // makes it match when the key is absent or its value is none of the
        // remaining ones (exclusion)
        var values = SplitValues(v);
        var mode = MapsforgeMatchMode.Positive;

        if (values.Contains("~"))
        {
            mode = MapsforgeMatchMode.Negation;
        }
        else if (values.Contains("-"))
        {
            mode = MapsforgeMatchMode.Exclusion;
        }

        var keys = anyKey ? [] : SplitValues(k);

        return new MFTagMatchExpression(keys, values, mode);
    }

    /// <summary>
    /// Splits a pipe separated value list of a rule condition into its tokens.
    /// </summary>
    private static string[] SplitValues(string? value)
    {
        return (value ?? "*").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Combines the conditions of the rule chain with the implied geometry
    /// condition of a drawing instruction into the filter of its layer. The
    /// element condition of the instructions affects their geometry: areas
    /// require closed ways (polygons), lines require linear geometries,
    /// circles require nodes.
    /// </summary>
    private static MFAllExpression CreateLayerFilter(RuleContext context, params IExpression[]? additionalConditions)
    {
        var conditions = context.Conditions;

        if (additionalConditions is { Length: > 0 })
        {
            conditions = [.. conditions, .. additionalConditions];
        }

        return conditions.Count == 0 ? MFAllExpression.Empty : new MFAllExpression(conditions);
    }

    /// <summary>
    /// Surname of a layer from the number of layers of the theme, so layer
    /// names stay unique within a theme (paints are keyed by their name).
    /// </summary>
    private static int LayerNameSuffix(RuleContext context)
    {
        return context.Theme.Layers.Count + 1;
    }

    /// <summary>
    /// Parses a symbol element into a symbol layer.
    /// </summary>
    private static MFSymbolLayerStyle ParseSymbolLayer(XElement element, RuleContext context, int counter)
    {
        var layer = new MFSymbolLayerStyle(
            $"symbol{counter}", CreateLayerFilter(context), context.ZoomMin, context.ZoomMax,
            element.Attribute("src")?.Value ?? string.Empty,
            ParseInt(element.Attribute("symbol-width")?.Value),
            ParseInt(element.Attribute("symbol-height")?.Value),
            ParseInt(element.Attribute("symbol-percent")?.Value),
            ParsePosition(element.Attribute("position")?.Value),
            ParseInt(element.Attribute("priority")?.Value) ?? 0,
            ParseDisplay(element.Attribute("display")?.Value),
            context.Cat,
            element.Attribute("id")?.Value)
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    /// <summary>
    /// Parses a caption element into a caption layer. Captions with a
    /// "symbol-id" reference the symbol layer with that id created in the same
    /// rule (their position is relative to its icon).
    /// </summary>
    private static MFCaptionLayerStyle ParseCaptionLayer(XElement element, RuleContext context, int counter,
        Dictionary<string, MFSymbolLayerStyle> symbolLayers)
    {
        var symbolId = element.Attribute("symbol-id")?.Value;

        MFSymbolLayerStyle? positionSymbol = null;
        MFSymbolLayerStyle? nearestSymbol = null;

        if (!string.IsNullOrEmpty(symbolId))
        {
            symbolLayers.TryGetValue(symbolId, out positionSymbol);
        }
        else if (ParsePosition(element.Attribute("position")?.Value) != MapsforgePosition.Auto)
        {
            // Captions without a symbol reference may still be positioned
            // relative to the first symbol of the rule (mapsforge resolves
            // "position" against the icon of the symbol drawn at the feature)
            nearestSymbol = symbolLayers.Values.FirstOrDefault();
        }

        var layer = new MFCaptionLayerStyle(
            $"caption{counter}", CreateLayerFilter(context), context.ZoomMin, context.ZoomMax,
            element.Attribute("k")?.Value ?? "name",
            ParseFontFamily(element.Attribute("font-family")?.Value),
            ParseFontStyle(element.Attribute("font-style")?.Value),
            (ParseFloat(element.Attribute("font-size")?.Value) ?? 20f),
            ParseColor(element.Attribute("fill")?.Value) ?? new Color(0, 0, 0, 255),
            ParseColor(element.Attribute("stroke")?.Value) ?? Colors.Empty,
            ParseFloat(element.Attribute("stroke-width")?.Value) ?? 0f,
            ParseFloat(element.Attribute("dy")?.Value) ?? 0f,
            ParsePosition(element.Attribute("position")?.Value),
            ParseInt(element.Attribute("priority")?.Value) ?? 0,
            ParseDisplay(element.Attribute("display")?.Value),
            symbolId,
            ParseTextTransform(element.Attribute("text-transform")?.Value),
            ParseInt(element.Attribute("text-wrap-width")?.Value))
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        layer.PositionSymbol = positionSymbol ?? nearestSymbol;
        layer.PositionSymbolSrc = layer.PositionSymbol?.Src;

        return layer;
    }

    private static MFCircleLayerStyle ParseCircleLayer(XElement element, RuleContext context, int counter)
    {
        // Circles are drawn at node geometries only
        var layer = new MFCircleLayerStyle(
            $"circle{counter}", CreateLayerFilter(context, new MFElementExpression(MapsforgeElement.Node)),
            context.ZoomMin, context.ZoomMax,
            ParseFloat(element.Attribute("radius")?.Value) ?? 0f,
            ParseColor(element.Attribute("fill")?.Value) ?? Colors.Empty,
            ParseColor(element.Attribute("stroke")?.Value) ?? Colors.Empty,
            ParseFloat(element.Attribute("stroke-width")?.Value) ?? 0f)
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    private static MFLineLayerStyle ParseLineLayer(XElement element, RuleContext context, int counter)
    {
        // Lines are drawn on linear geometries only
        var layer = new MFLineLayerStyle(
            $"line{counter}", CreateLayerFilter(context, new MFClosedExpression(MapsforgeClosed.No)),
            context.ZoomMin, context.ZoomMax,
            ParseColor(element.Attribute("stroke")?.Value) ?? new Color(0, 0, 0, 255),
            ParseFloat(element.Attribute("stroke-width")?.Value) ?? 0f,
            ParseDashArray(element.Attribute("stroke-dasharray")?.Value),
            ParseStrokeCap(element.Attribute("stroke-linecap")?.Value),
            ParseStrokeJoin(element.Attribute("stroke-linejoin")?.Value),
            element.Attribute("src")?.Value,
            ParseFloat(element.Attribute("dy")?.Value) ?? 0f)
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    private static MFFillLayerStyle ParseAreaLayer(XElement element, RuleContext context, int counter)
    {
        // Areas are drawn on closed geometries only (polygons)
        var layer = new MFFillLayerStyle(
            $"area{counter}", CreateLayerFilter(context, new MFClosedExpression(MapsforgeClosed.Yes)),
            context.ZoomMin, context.ZoomMax,
            ParseColor(element.Attribute("fill")?.Value) ?? new Color(0, 0, 0, 255),
            ParseColor(element.Attribute("stroke")?.Value) ?? Colors.Empty,
            ParseFloat(element.Attribute("stroke-width")?.Value) ?? 0f,
            element.Attribute("src")?.Value)
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    private static MFLineSymbolLayerStyle ParseLineSymbolLayer(XElement element, RuleContext context, int counter)
    {
        // Line symbols are placed along geometries (ways), not nodes
        var layer = new MFLineSymbolLayerStyle(
            $"lineSymbol{counter}", CreateLayerFilter(context, new MFElementExpression(MapsforgeElement.Way)),
            context.ZoomMin, context.ZoomMax,
            element.Attribute("src")?.Value ?? string.Empty,
            ParseInt(element.Attribute("symbol-width")?.Value),
            ParseInt(element.Attribute("symbol-height")?.Value),
            ParseInt(element.Attribute("symbol-percent")?.Value),
            ParseBool(element.Attribute("repeat")?.Value) ?? false,
            ParseFloat(element.Attribute("repeat-gap")?.Value) ?? 200f,
            ParseFloat(element.Attribute("repeat-start")?.Value) ?? 30f,
            ParseBool(element.Attribute("rotate")?.Value) ?? true,
            element.Attribute("symbol-orientation")?.Value ?? "right",
            ParseFloat(element.Attribute("dy")?.Value) ?? 0f,
            ParsePosition(element.Attribute("position")?.Value),
            ParseInt(element.Attribute("priority")?.Value) ?? 0,
            ParseDisplay(element.Attribute("display")?.Value))
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    private static MFPathTextLayerStyle ParsePathTextLayer(XElement element, RuleContext context, int counter)
    {
        // Path text is placed along geometries (ways), not nodes
        var layer = new MFPathTextLayerStyle(
            $"pathText{counter}", CreateLayerFilter(context, new MFElementExpression(MapsforgeElement.Way)),
            context.ZoomMin, context.ZoomMax,
            element.Attribute("k")?.Value ?? "name",
            ParseFontFamily(element.Attribute("font-family")?.Value),
            ParseFontStyle(element.Attribute("font-style")?.Value),
            (ParseFloat(element.Attribute("font-size")?.Value) ?? 20f),
            ParseColor(element.Attribute("fill")?.Value) ?? new Color(0, 0, 0, 255),
            ParseColor(element.Attribute("stroke")?.Value) ?? Colors.Empty,
            ParseFloat(element.Attribute("stroke-width")?.Value) ?? 0f,
            ParseBool(element.Attribute("repeat")?.Value) ?? false,
            ParseFloat(element.Attribute("repeat-gap")?.Value) ?? 200f,
            ParseFloat(element.Attribute("repeat-start")?.Value) ?? 30f,
            ParseBool(element.Attribute("rotate")?.Value) ?? true,
            ParseFloat(element.Attribute("dy")?.Value) ?? 0f,
            ParseInt(element.Attribute("priority")?.Value) ?? 0,
            ParseDisplay(element.Attribute("display")?.Value),
            element.Attribute("text-orientation")?.Value ?? "auto",
            ParseTextTransform(element.Attribute("text-transform")?.Value))
        {
            Source = context.Theme.Source
        };

        SetBaseAttributes(layer, context);

        return layer;
    }

    /// <summary>
    /// Sets the theme wide attributes (base-stroke-width, base-text-size) and
    /// the rule category of the layer.
    /// </summary>
    private static void SetBaseAttributes(MFLayerStyle layer, RuleContext context)
    {
        layer.BaseStrokeWidth = context.Theme.BaseStrokeWidth;
        layer.BaseTextSize = context.Theme.BaseTextSize;
        layer.Cat = context.Cat;
    }

    /// <summary>
    /// Parses a position attribute value ("auto", "center", "above_left", ...).
    /// </summary>
    private static MapsforgePosition ParsePosition(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "center" => MapsforgePosition.Center,
            "below" => MapsforgePosition.Below,
            "below_left" => MapsforgePosition.BelowLeft,
            "below_right" => MapsforgePosition.BelowRight,
            "above" => MapsforgePosition.Above,
            "above_left" => MapsforgePosition.AboveLeft,
            "above_right" => MapsforgePosition.AboveRight,
            "left" => MapsforgePosition.Left,
            "right" => MapsforgePosition.Right,
            "auto" or null or "" => MapsforgePosition.Auto,
            _ => MapsforgePosition.Auto
        };
    }

    /// <summary>
    /// Parses a display attribute value ("ifspace", "order", "always", "never").
    /// </summary>
    private static MapsforgeDisplay ParseDisplay(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "order" => MapsforgeDisplay.Order,
            "always" => MapsforgeDisplay.Always,
            "never" => MapsforgeDisplay.Never,
            "ifspace" or null or "" => MapsforgeDisplay.IfSpace,
            _ => MapsforgeDisplay.IfSpace
        };
    }

    /// <summary>
    /// Parses a font-family attribute value ("default", "monospace",
    /// "sans_serif", "serif").
    /// </summary>
    private static MapsforgeFontFamily ParseFontFamily(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "monospace" => MapsforgeFontFamily.Monospace,
            "sans_serif" => MapsforgeFontFamily.SansSerif,
            "serif" => MapsforgeFontFamily.Serif,
            "default" or null or "" => MapsforgeFontFamily.Default,
            _ => MapsforgeFontFamily.Default
        };
    }

    /// <summary>
    /// Parses a font-style attribute value ("normal", "bold", "italic",
    /// "bold_italic").
    /// </summary>
    private static MapsforgeFontStyle ParseFontStyle(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "bold" => MapsforgeFontStyle.Bold,
            "italic" => MapsforgeFontStyle.Italic,
            "bold_italic" => MapsforgeFontStyle.BoldItalic,
            "normal" or null or "" => MapsforgeFontStyle.Normal,
            _ => MapsforgeFontStyle.Normal
        };
    }

    /// <summary>
    /// Parses a text-transform attribute value ("none", "uppercase",
    /// "lowercase", "capitalize").
    /// </summary>
    private static MapsforgeTextTransform ParseTextTransform(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "uppercase" => MapsforgeTextTransform.Uppercase,
            "lowercase" => MapsforgeTextTransform.Lowercase,
            "capitalize" => MapsforgeTextTransform.Capitalize,
            "none" or null or "" => MapsforgeTextTransform.None,
            _ => MapsforgeTextTransform.None
        };
    }

    /// <summary>
    /// Parses a stroke-dasharray attribute value (a comma separated list of
    /// dash and gap lengths).
    /// </summary>
    private static float[]? ParseDashArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var lengths = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseFloat)
            .ToList();

        if (lengths.Count == 0 || lengths.Any(l => !l.HasValue))
        {
            return null;
        }

        // Mapsforge strokes an odd number of lengths by repeating the list
        return lengths.Count % 2 == 0
            ? lengths.Select(l => l!.Value).ToArray()
            : [.. lengths.Select(l => l!.Value), lengths[0].Value];
    }

    /// <summary>
    /// Parses a stroke-linecap attribute value ("butt", "round", "square").
    /// </summary>
    private static StrokeCap ParseStrokeCap(string? value)
    {
        // Mapsforge theme spec: stroke-linecap defaults to "round" (falls
        // back to round for unknown values as well -- see mapsforge's
        // Line.parseCap); only an explicit "butt" disables the caps.
        return value?.ToLowerInvariant() switch
        {
            "butt" => StrokeCap.Butt,
            "square" => StrokeCap.Square,
            _ => StrokeCap.Round
        };
    }

    /// <summary>
    /// Parses a stroke-linejoin attribute value ("miter", "round", "bevel").
    /// </summary>
    private static StrokeJoin ParseStrokeJoin(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "round" => StrokeJoin.Round,
            "bevel" => StrokeJoin.Bevel,
            "miter" or null or "" => StrokeJoin.Miter,
            _ => StrokeJoin.Miter
        };
    }

    /// <summary>
    /// Parses a color from its hex notation: "#RGB", "#RRGGBB" and
    /// "#AARRGGBB" (alpha first, as in the rendertheme files).
    /// </summary>
    /// <param name="value">Hex color value, or null.</param>
    /// <returns>The parsed color, or null when no color can be parsed.</returns>
    private static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Trim();

        if (!hex.StartsWith('#'))
        {
            return null;
        }

        var digits = hex[1..];

        switch (digits.Length)
        {
            case 3:
            {
                // "#RGB" expands to "#FFRRGGBB"
                return ParseHexByte(digits, 0, 2, out var r)
                    && ParseHexByte(digits, 1, 2, out var g)
                    && ParseHexByte(digits, 2, 2, out var b)
                        ? new Color(r, g, b, 255)
                        : null;
            }
            case 6:
            {
                // "#RRGGBB" expands with full alpha
                return ParseHexByte(digits, 0, 2, out var r)
                    && ParseHexByte(digits, 2, 2, out var g)
                    && ParseHexByte(digits, 4, 2, out var b)
                        ? new Color(r, g, b, 255)
                        : null;
            }
            case 8:
            {
                // "#AARRGGBB"
                return ParseHexByte(digits, 0, 2, out var a)
                    && ParseHexByte(digits, 2, 2, out var r)
                    && ParseHexByte(digits, 4, 2, out var g)
                    && ParseHexByte(digits, 6, 2, out var b)
                        ? new Color(r, g, b, a)
                        : null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Parses two hex digits at the given position of a string into a byte.
    /// </summary>
    private static bool ParseHexByte(string value, int position, int length, out byte result)
    {
        return byte.TryParse(value.Substring(position, length),
            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Parses an int attribute value (invariant culture), or null.
    /// </summary>
    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    /// <summary>
    /// Parses a float attribute value (invariant culture), or null.
    /// </summary>
    private static float? ParseFloat(string? value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    /// <summary>
    /// Parses a bool attribute value, or null for missing values.
    /// </summary>
    private static bool? ParseBool(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => null
        };
    }

    /// <summary>
    /// The conditions and attributes of a rule chain while recursively parsing
    /// the rules of a render theme.
    /// </summary>
    private class RuleContext
    {
        /// <summary>Minimal zoom level of the rule chain (inclusive).</summary>
        public int ZoomMin { get; set; }

        /// <summary>Maximal zoom level of the rule chain (inclusive).</summary>
        public int ZoomMax { get; set; }

        /// <summary>Category of the rule chain ("cat" attribute, inherited).</summary>
        public string Cat { get; set; } = string.Empty;

        /// <summary>Filter conditions of the rule chain so far.</summary>
        public required List<IExpression> Conditions { get; init; }

        /// <summary>The theme the layers are added to.</summary>
        public required MFRenderTheme Theme { get; init; }

        /// <summary>Sprite factory resolving icon and pattern sources.</summary>
        public required MFSpriteFactory SpriteFactory { get; init; }
    }
}
