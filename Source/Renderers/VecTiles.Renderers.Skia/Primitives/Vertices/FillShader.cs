using System.Diagnostics;
using SkiaSharp;
using VecTiles.Renderers.Skia.Extensions;
using VecTiles.Common.Interfaces;

namespace VecTiles.Renderers.Skia.Primitives.Vertices;

// ============================================================================
// Unified fill shader: solid color OR atlas sprite via a child shader.
// One SKRuntimeEffect serves both cases; uUseTexture selects the path.
// ============================================================================
/// <summary>
/// Runtime effect shader for fill layers. A single effect serves solid color
/// and atlas sprite patterns; the mode is selected per Update call. The fill
/// color is multiplied by global alpha in the shader, so the SKPaint uses white.
/// </summary>
public class FillShader(SKRuntimeEffect effect) : IDisposable
{
    private readonly SKRuntimeEffect _effect = effect;

    /// <summary>
    /// Gets the SkSL source of the fill shader.
    /// </summary>
    public const string SkSl = """
        uniform shader uTexture;   // child: sprite image (or 1x1 white fallback)
        uniform float4 uColor;     // solid fill color (premultiplied in shader)
        uniform float2 uSprite;    // sprite size in atlas pixels
        uniform float2 uPattern;   // size of ONE tile in world units
        uniform float  uRepeat;    // 1: tile infinitely, 0: stretch once
        uniform float  uAlpha;     // global opacity 0..1
        uniform float  uUseTexture; // 1: sample sprite, 0: solid uColor

        half4 main(float2 p) {
            // p = world position (mesh emits u = x, v = y)
            if (uUseTexture > 0.5) {
                float2 t = p / uPattern;
                if (uRepeat < 0.5) {
                    t = clamp(t, 0.0, 1.0);          // single stretch
                }
                // No fract(), no half-texel inset: the child shader wraps
                // via SKShaderTileMode.Repeat and samples full texels, so
                // tiles stay seamless and uncompressed.
                float2 uv = t * uSprite;

                half4 c = uTexture.eval(uv);
                return c * uAlpha; // Premultiplied
            }
            // Solid path: premultiply on the fly.
            return half4(uColor.rgb * uColor.a, uColor.a) * uAlpha; // Not premultiplied
        }
        """;

    // Shared 1x1 white fallback shader so the child slot is always bound,
    // even for solid fills (an unbound child would sample as transparent).
    private static readonly Lazy<SKShader> _whiteShader =
        new(() => CreateWhiteShader(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared 1x1 white fallback shader bound to the child slot when
    /// no texture is used (an unbound child would sample as transparent).
    /// </summary>
    public static SKShader WhiteShader => _whiteShader.Value;

    private static SKShader CreateWhiteShader()
    {
        var info = new SKImageInfo(1, 1, SKColorType.Rgba8888);
        using var bmp = new SKBitmap(info);
        bmp.Erase(SKColors.White);
        return SKImage.FromBitmap(bmp).ToShader(SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            new SKSamplingOptions(SKFilterMode.Linear));
    }

    /// <summary>
    /// Disposes the shared white shader when it was created, e.g. on
    /// application shutdown. Do not call it from instance Dispose().
    /// </summary>
    public static void DisposeSharedResources()
    {
        if (_whiteShader.IsValueCreated) _whiteShader.Value.Dispose();
    }

    // Cache the child shader per sprite: ToShader() allocates a new GPU
    // object on every call -- do not create it per frame.
    private ISprite? _lastSprite;
    private SKShader? _spriteShader;

    /// <summary>
    /// Creates the shader. One effect handles solid and textured fills;
    /// the mode is chosen per Update call without re-tessellating.
    /// </summary>
    public static FillShader Create()
    {
        var effect = SKRuntimeEffect.CreateShader(FillShader.SkSl, out string? err)
                  ?? throw new InvalidOperationException($"SkSL error: {err}");
        return new FillShader(effect);
    }

    /// <summary>Solid color fill.</summary>
    public SKShader Update(SKColor color, float alpha = 1f)
    {
        var uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["uUseTexture"] = 0f,
            ["uColor"]      = new[] { color.Red / 255f, color.Green / 255f,
                                      color.Blue / 255f, color.Alpha / 255f },
            ["uAlpha"]      = alpha,
        };
        var children = new SKRuntimeEffectChildren(_effect)
        {
            ["uTexture"] = WhiteShader,   // bound but unused in solid mode
        };
        return _effect.ToShader(uniforms, children);
    }

    /// <summary>Textured fill from an atlas sprite.</summary>
    /// <param name="patternWidthWorldPx">
    ///   Pattern period in world px (tile px). Default: the sprite at its
    ///   native atlas size (one sprite per tile, sampled 1:1).
    /// </param>
    /// <param name="patternHeightWorldPx">
    ///   Vertical period; -1 means "same as patternWidthWorldPx".
    /// </param>
    public SKShader Update(ISprite sprite, bool repeat, float alpha = 1f, 
        float patternWidthWorldPx = -1f,
        float patternHeightWorldPx = -1f)
    {
        // Vector sprites (SVG patterns) are rasterized at their display size,
        // raster sprites are decoded and cut out of their atlas
        var image = sprite.GetImage();
        if (image == null)
        {
            // Sprite cannot be drawn: keep sampling the white shader
            return Update(SKColors.White, alpha);
        }

        if (!ReferenceEquals(sprite, _lastSprite))
        {
            // Sprite changed: rebuild the cached child shader. The image is
            // owned by the sprite, so do not dispose it here -- only the
            // shader wrapper is ours.
            _spriteShader?.Dispose();
            _spriteShader = image.ToShader(
                SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, new SKSamplingOptions(SKFilterMode.Linear));
            _lastSprite = sprite;
        }

        // Default: one sprite per tile at native size.
        float pw = patternWidthWorldPx > 0 ? patternWidthWorldPx : sprite.Width;
        float ph = patternHeightWorldPx > 0 ? patternHeightWorldPx : sprite.Height;

        var uniforms = new SKRuntimeEffectUniforms(_effect)
        {
            ["uUseTexture"] = 1f,
            ["uSprite"]     = new[] { (float)sprite.Width, (float)sprite.Height },
            ["uPattern"]    = new[] { pw, ph },
            ["uRepeat"]     = repeat ? 1f : 0f,
            ["uAlpha"]      = alpha,
        };
        var children = new SKRuntimeEffectChildren(_effect)
        {
            // No scale matrix: uPattern fully controls the world size.
            ["uTexture"] = _spriteShader!,
        };
        return _effect.ToShader(uniforms, children);
    }

    /// <summary>
    /// Disposes the cached sprite shader and the runtime effect. Sprite images
    /// themselves are owned by their sprites, not by this shader.
    /// </summary>
    public void Dispose()
    {
        // Sprite images are owned by the sprites, not by this shader.
        _spriteShader?.Dispose();
        _effect.Dispose();
    }
}