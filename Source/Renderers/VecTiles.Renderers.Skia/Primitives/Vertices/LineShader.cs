using SkiaSharp;

namespace VecTiles.Renderers.Skia.Primitives.Vertices;

public class LineShader(SKRuntimeEffect effect, float maxWidth) : IDisposable
{
    // Style calibration factor: 1 = dash length equals the stroke width
    // (Mapbox spec semantics). If your reference renderer shows a different
    // ratio (e.g. dashes 4x the stroke width), set the measured factor here.
    private const float DashUnitScale = 1f;

    private readonly SKRuntimeEffect _effect = effect;
    private readonly float _maxHalfWidth = maxWidth / 2f;
    private float[] _dashArray = [];
    private SKImage? _dashImage;
    private float _dashTexelWidth;
    private SKShader? _dashShader;

    private const string SkSl = """
                                uniform shader uDashPattern;  // 1D dash coverage texture (R channel)
                                uniform float uMaxHalfWidth;
                                uniform float uHalfWidth;
                                uniform float4 uColor;
                                uniform float uDashTexelsPerWorldPx;

                                half4 main(float2 p) {
                                    // Sample the dash pattern at the arc length. Linear filtering
                                    // gives the 1px AA transition at every dash boundary for free.
                                    // +0.5 px shift so a line starting at a dash boundary sits fully
                                    // inside the dash (same trick as before).
                                    // Dash pattern sampled in texel space. One repeat =
                                    // uDashPeriodTexels / uDashTexelsPerUnit pattern units, scaled into
                                    // tile px by the caller via uDashPeriodTexels. Anchored in tile
                                    // space: zooming scales geometry and pattern together, nothing crawls.
                                    float dist = abs(p.x) * uMaxHalfWidth;
                                    float dashCoverage = uDashPattern.eval(float2(p.y * uDashTexelsPerWorldPx, 0.5)).r;
                                    float coverage = 1.0 - smoothstep(uHalfWidth - 0.5, uHalfWidth + 0.5, dist);
                                    coverage *= dashCoverage;
                                    if (coverage <= 0.0) return half4(0.0);
                                    return half4(uColor.rgb * uColor.a, uColor.a) * coverage;
                                }
                                """;
    
    // 1x1 white dummy image shader as the solid fallback for the dash child
    // shader. Lazy: created on first access, thread-safe.
    private static readonly Lazy<SKShader> _whiteShader =
        new(CreateWhiteShader, LazyThreadSafetyMode.ExecutionAndPublication);

    private static SKShader WhiteShader => _whiteShader.Value;

    private static SKShader CreateWhiteShader()
    {
        var info = new SKImageInfo(1, 1, SKColorType.Rgba8888);
        using var bmp = new SKBitmap(info);
        bmp.Erase(SKColors.White);
        return SKImage.FromBitmap(bmp).ToShader(SKShaderTileMode.Repeat,
            SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear));
    }

    // Optional: release the shared static image (e.g. in tests).
    // Do NOT call this from instance Dispose() -- other instances
    // would break.
    public void Dispose()
    {
        // Dispose the pattern shader only if it is instance-owned.
        // The shared WhiteShader must stay alive for other instances.
        if (_dashShader is not null && !ReferenceEquals(_dashShader, WhiteShader))
            _dashShader.Dispose();
        _dashImage?.Dispose();   // only if this instance created it (see below)
        _effect.Dispose();
    }

    public static void DisposeSharedResources()
    {
        if (_whiteShader.IsValueCreated) 
            _whiteShader.Value.Dispose();
    }
    
    /// <summary>
    /// Creates the shader. Pass dashOn &lt;= 0 (or leave at 0) for a solid
    /// line; otherwise dashOn/dashOff/phase are the dash pattern in pixels,
    /// changeable at runtime WITHOUT re-tessellating the mesh.
    /// </summary>
    public static LineShader Create(float maxWidth)
    {
        var effect = SKRuntimeEffect.CreateShader(LineShader.SkSl, out string? err)
                  ?? throw new InvalidOperationException($"SkSL error: {err}");
        var shader = new LineShader(effect, maxWidth);

        return shader;
    }

    /// <param name="strokeWidth">
    ///     Current width in tile px (drives the stroke).
    /// </param>
    /// <param name="color">Color for line</param>
    /// <param name="dashArray">Array for dash pattern with on-off-values</param>
    /// <param name="dashRefHalfWidth">
    ///   Half width in tile px at the SETTLED integer zoom level
    ///   (style width at zInt / 2 / scale at zInt). Constant during zoom
    ///   animations -> dash pattern stays glued to the geometry; changes
    ///   in discrete steps with the zoom level.
    /// </param>
    public SKShader Update(float strokeWidth, 
        SKColor color, 
        float[]? dashArray = null,
        float dashRefHalfWidth = 0f)
    {
        var dashTexelsPerWorldPx = 0f;

        if (dashArray is null || dashArray.Length < 2)
        {
            // Solid: no pattern. WhiteShader returns 1 everywhere, so the
            // sampling coordinate is irrelevant -- set it to 0.
            _dashImage = null;
            _dashShader = null;
            _dashTexelWidth = 1f;
            _dashArray = [];
            dashTexelsPerWorldPx = 0f;
        }
        else if (!dashArray.AsSpan().SequenceEqual(_dashArray))
        {
            // Create new dash pattern
            _dashShader?.Dispose();
            _dashImage?.Dispose();
            _dashArray = (float[])dashArray.Clone();  // caller may mutate it
            (_dashImage, _dashTexelWidth) = CreateDashPatternImage(dashArray);
            if (_dashImage is not null)
            {
                _dashShader = _dashImage.ToShader(SKShaderTileMode.Repeat,
                    SKShaderTileMode.Clamp,
                    new SKSamplingOptions(SKFilterMode.Linear));
            }
        }

        if (_dashShader is not null)
        {
            float total = 0f;
            foreach (var v in _dashArray) total += MathF.Max(v, 0f);

            // uHalfWidth drives the stroke (animated); the dash period uses
            // dashRefHalfWidth (frozen per zoom level) so the pattern does
            // not crawl while zooming.
            dashTexelsPerWorldPx = _dashTexelWidth
                                   / (total * dashRefHalfWidth * 2f);
        }
        
        var uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["uMaxHalfWidth"] = _maxHalfWidth,
            ["uHalfWidth"]    = strokeWidth / 2f,
            ["uColor"]        = new[] { color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f },
            ["uDashTexelsPerWorldPx"] = dashTexelsPerWorldPx,
        };

        var children = new SKRuntimeEffectChildren(effect)
        {
            ["uDashPattern"] = _dashShader ?? WhiteShader,
        };

        return _effect.ToShader(uniforms, children);
    }
    
    // Build the pattern texture in PATTERN UNITS (not pixels).
    // Resolution factor k controls edge quality (e.g. k = 4 texels per unit).
    private static (SKImage?, float) CreateDashPatternImage(float[] pattern, int texelsPerUnit = 4)
    {
        float dashLength = 0f;
        foreach (var v in pattern) 
            dashLength += MathF.Max(v, 0f);
        if (pattern.Length < 2 || dashLength <= 0f)
            return (null, 1);

        int width = Math.Max(1, (int)MathF.Ceiling(dashLength * texelsPerUnit));
        var info = new SKImageInfo(width, 1, SKColorType.Rgba8888);
        using var bmp = new SKBitmap(info);

        for (int x = 0; x < width; x++)
        {
            // We set the coverage directly in texel space!
            // pos is between 0 and width (texel units)
            float pos = x + 0.5f; 
            float t = pos;
            bool on = true;
            float currentCoverage = 0f;

            foreach (var len in pattern)
            {
                // Convert interval lenght into texel
                float l = MathF.Max(len, 0f) * texelsPerUnit; 
                if (t < l)
                {
                    // Smooth edge of exact 1 texel width (0.5 texel radius)
                    float distToEdge = MathF.Min(t, l - t);
                    float edgeFactor = Math.Clamp(distToEdge + 0.5f, 0f, 1f);
                    currentCoverage = on ? edgeFactor : (1f - edgeFactor);
                    break;
                }
                t -= l;
                on = !on;
            }

            byte c = (byte)(currentCoverage * 255f);
            bmp.SetPixel(x, 0, new SKColor(c, c, c, 255));
        }

        // Important: We have to tell the shader the real pixel width!
        return (SKImage.FromBitmap(bmp), width);
    }
}
