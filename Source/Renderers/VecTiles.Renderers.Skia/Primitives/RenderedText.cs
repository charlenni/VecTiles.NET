using NetTopologySuite.Geometries;
using Topten.RichTextKit;
using VecTiles.Common.Primitives;

namespace VecTiles.Renderers.Skia.Primitives;

public class RenderedText
{
    public RenderedText(string text, Style? style = null)
    {
        TextStyle = style ?? new Style();
        
        Text = new TextBlock();
        Text.AddText(text, TextStyle);    
    }
    
    public TextBlock Text { get; }
    
    public Style TextStyle { get; }
    
    public float MeasuredWidth { get; set; }
    
    public float MeasuredHeight { get; set; }
    
    public float LeftRightCorrection { get; set; }
    
    public EvaluationContext LastContext { get; set; } = EvaluationContext.Empty;

    public Envelope LastEnvelope { get; set; } = new Envelope();
}