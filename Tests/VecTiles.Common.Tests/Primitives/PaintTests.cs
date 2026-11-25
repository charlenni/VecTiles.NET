using NetTopologySuite.Features;
using VecTiles.Common.Enums;
using VecTiles.Common.Extensions;
using VecTiles.Common.Primitives;

namespace VecTiles.Common.Tests.Primitives;

/// <summary>
/// Tests for the data-driven paint: the snapshot of a paint for a feature
/// context fixes the evaluated values, so the paint no longer changes at
/// draw time, and reports whether any property is data-driven.
/// </summary>
public class PaintTests
{
    /// <summary>
    /// A paint without variable functions is not data-driven, and its
    /// snapshot equals the paint.
    /// </summary>
    [Fact]
    public void Snapshot_ConstantPaint_KeepsValues()
    {
        var paint = new Paint("test");
        paint.SetFixColor(Colors.Red);
        paint.SetFixOpacity(0.5f);
        paint.SetFixStrokeWidth(3f);
        paint.SetFixStyle(PaintStyle.Stroke);

        Assert.False(paint.IsDataDriven);

        var snapshot = paint.SnapshotFor(new EvaluationContext(16));

        Assert.False(snapshot.IsDataDriven);
        Assert.Equal(Colors.Red.WithAlpha((byte)(Colors.Red.A * 0.5f)), snapshot.Color);
        Assert.Equal(3f, snapshot.StrokeWidth);
        Assert.Equal(PaintStyle.Stroke, snapshot.Style);
    }

    /// <summary>
    /// A paint with a variable function is data-driven, and the snapshot
    /// freezes the values evaluated for the given feature context.
    /// </summary>
    [Fact]
    public void Snapshot_DataDrivenPaint_FreezesEvaluatedValues()
    {
        var (paint, featureRed, featureBlue) = DataDrivenPaint();

        Assert.True(paint.IsDataDriven);

        var redSnapshot = (Paint)paint.SnapshotFor(featureRed);
        var blueSnapshot = (Paint)paint.SnapshotFor(featureBlue);

        Assert.False(redSnapshot.IsDataDriven);
        Assert.Equal(Colors.Red, redSnapshot.Color);
        Assert.Equal(5f, redSnapshot.StrokeWidth);
        // GetMaxStrokeWidth freezes the bake width semantics of the variable
        // branch: the maximum of the width at the zoom of the context (with
        // the feature attributes) and at the next zoom level (without them)
        Assert.Equal(7f, redSnapshot.GetMaxStrokeWidth(featureRed));
        Assert.Equal(Colors.Blue, blueSnapshot.Color);
        Assert.Equal(7f, blueSnapshot.StrokeWidth);
    }

    /// <summary>
    /// The snapshot paint keeps its frozen values regardless of the context
    /// of later Update calls, e.g. the feature-less draw time context.
    /// </summary>
    [Fact]
    public void Snapshot_IsFrozenAgainstUpdate()
    {
        var (paint, featureRed, _) = DataDrivenPaint();
        var snapshot = paint.SnapshotFor(featureRed);

        // Draw time update with a different zoom and without a feature
        snapshot.Update(new EvaluationContext(featureRed.Zoom + 2, featureRed.Scale));

        Assert.Equal(Colors.Red, snapshot.Color);
        Assert.Equal(5f * featureRed.Scale, snapshot.StrokeWidth);
    }

    /// <summary>
    /// The snapshot composes the alpha of the color with the evaluated
    /// opacity, like the evaluation of the paint would.
    /// </summary>
    [Fact]
    public void Snapshot_ComposesColorWithOpacity()
    {
        var paint = new Paint("test");
        paint.SetVariableColor(_ => Colors.Red);
        paint.SetVariableOpacity(_ => 0.5f);

        var snapshot = paint.SnapshotFor(new EvaluationContext(16));

        Assert.Equal(Colors.Red.WithAlpha((byte)(Colors.Red.A * 0.5f)), snapshot.Color);
    }

    /// <summary>
    /// Gets a data-driven paint (color and stroke width from the feature
    /// attributes) and two feature contexts evaluating differently.
    /// </summary>
    private static (Paint Paint, EvaluationContext Red, EvaluationContext Blue) DataDrivenPaint()
    {
        var paint = new Paint("test");
        paint.SetVariableColor(context =>
            context.Attributes?.GetNames().Contains("red") == true ? Colors.Red : Colors.Blue);
        paint.SetVariableStrokeWidth(context =>
            context.Attributes?.GetNames().Contains("red") == true ? 5f : 7f);

        return (paint,
            new EvaluationContext(16, attributes: new AttributesTable(new Dictionary<string, object> { ["red"] = true })),
            new EvaluationContext(16, attributes: new AttributesTable(new Dictionary<string, object> { ["blue"] = true })));
    }
}
