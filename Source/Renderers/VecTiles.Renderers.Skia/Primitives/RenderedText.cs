using NetTopologySuite.Geometries;
using Topten.RichTextKit;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Primitives;

/// <summary>
/// Shaped text of a symbol: the RichTextKit text block together with the
/// style it was created with and measurement results used for placement.
/// </summary>
public class RenderedText
{
    /// <summary>
    /// Create an instance from the given text and style.
    /// </summary>
    /// <param name="text">Text to shape.</param>
    /// <param name="style">Text style used for shaping and drawing, or null for defaults.</param>
    public RenderedText(string text, Style? style = null)
    {
        TextStyle = style ?? new Style();
        
        Text = new TextBlock();
        Text.AddText(text, TextStyle);    
    }
    
    /// <summary>
    /// Gets the shaped text block.
    /// </summary>
    public TextBlock Text { get; }
    
    /// <summary>
    /// Gets the text style the text block was created with.
    /// </summary>
    public Style TextStyle { get; }
    
    /// <summary>
    /// Gets or sets the measured width of the text in pixels.
    /// </summary>
    public float MeasuredWidth { get; set; }
    
    /// <summary>
    /// Gets or sets the measured height of the text in pixels.
    /// </summary>
    public float MeasuredHeight { get; set; }
    
    /// <summary>
    /// Gets or sets the horizontal correction between the left and right edge
    /// of the text, used to center rotated line labels.
    /// </summary>
    public float LeftRightCorrection { get; set; }
    
    /// <summary>
    /// Gets or sets the evaluation context of the last placement of this text.
    /// </summary>
    public EvaluationContext LastContext { get; set; } = EvaluationContext.Empty;

    /// <summary>
    /// Gets or sets the bounding envelope of the last placement of this text.
    /// </summary>
    public Envelope LastEnvelope { get; set; } = new Envelope();
}