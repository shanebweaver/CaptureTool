using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class ModifyShapeOperationTests
{
    [TestMethod]
    public void RedoAndUndo_ForRectangle_AppliesEveryRectangleProperty()
    {
        var shape = new RectangleDrawable(new Vector2(1, 2), new Size(3, 4), Color.Red, Color.Blue, 5);
        var oldState = new ModifyShapeOperation.ShapeState(shape);
        var newState = oldState with
        {
            Offset = new Vector2(6, 7),
            Size = new Size(8, 9),
            StrokeColor = Color.Green,
            FillColor = Color.Yellow,
            StrokeWidth = 10,
        };
        int invalidations = 0;
        var operation = new ModifyShapeOperation(shape, oldState, newState, () => invalidations++);

        operation.Redo();

        AssertRectangleState(shape, newState);

        operation.Undo();

        AssertRectangleState(shape, oldState);
        Assert.AreEqual(2, invalidations);
    }

    [TestMethod]
    public void RedoAndUndo_ForLine_AppliesLineProperties()
    {
        var shape = new LineDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 5);
        var oldState = new ModifyShapeOperation.ShapeState(shape);
        var newState = oldState with
        {
            Offset = new Vector2(6, 7),
            EndPoint = new Vector2(8, 9),
            StrokeColor = Color.Green,
            StrokeWidth = 10,
        };
        var operation = new ModifyShapeOperation(shape, oldState, newState);

        operation.Redo();

        Assert.AreEqual(newState.Offset, shape.Offset);
        Assert.AreEqual(newState.EndPoint, shape.EndPoint);
        Assert.AreEqual(newState.StrokeColor, shape.StrokeColor);
        Assert.AreEqual(newState.StrokeWidth, shape.StrokeWidth);

        operation.Undo();

        Assert.AreEqual(oldState.Offset, shape.Offset);
        Assert.AreEqual(oldState.EndPoint, shape.EndPoint);
        Assert.AreEqual(oldState.StrokeColor, shape.StrokeColor);
        Assert.AreEqual(oldState.StrokeWidth, shape.StrokeWidth);
    }

    [TestMethod]
    public void RedoAndUndo_ForArrow_AppliesArrowProperties()
    {
        var shape = new ArrowDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 5);
        var oldState = new ModifyShapeOperation.ShapeState(shape);
        var newState = oldState with
        {
            Offset = new Vector2(6, 7),
            EndPoint = new Vector2(8, 9),
            StrokeColor = Color.Green,
            StrokeWidth = 10,
        };
        var operation = new ModifyShapeOperation(shape, oldState, newState);

        operation.Redo();

        Assert.AreEqual(newState.Offset, shape.Offset);
        Assert.AreEqual(newState.EndPoint, shape.EndPoint);
        Assert.AreEqual(newState.StrokeColor, shape.StrokeColor);
        Assert.AreEqual(newState.StrokeWidth, shape.StrokeWidth);

        operation.Undo();

        Assert.AreEqual(oldState.Offset, shape.Offset);
        Assert.AreEqual(oldState.EndPoint, shape.EndPoint);
        Assert.AreEqual(oldState.StrokeColor, shape.StrokeColor);
        Assert.AreEqual(oldState.StrokeWidth, shape.StrokeWidth);
    }

    [TestMethod]
    public void RedoAndUndo_ForText_AppliesTextProperties()
    {
        var shape = new TextDrawable(new Vector2(1, 2), new Size(3, 4), "old", Color.Red, Color.Blue, "Arial", 12);
        var oldState = new ModifyShapeOperation.ShapeState(shape);
        var newState = oldState with
        {
            Offset = new Vector2(6, 7),
            Size = new Size(8, 9),
            Text = "new",
            TextColor = Color.Green,
            TextBackgroundColor = Color.Yellow,
            FontFamily = "Segoe UI Variable",
            FontSize = 18,
        };
        var operation = new ModifyShapeOperation(shape, oldState, newState);

        operation.Redo();

        AssertTextState(shape, newState);

        operation.Undo();

        AssertTextState(shape, oldState);
    }

    [TestMethod]
    public void ShapeState_ForUnknownDrawable_UsesDefaultValues()
    {
        var state = new ModifyShapeOperation.ShapeState(new UnknownDrawable { Offset = new Vector2(4, 5) });

        Assert.AreEqual(default, state.Offset);
        Assert.AreEqual(Size.Empty, state.Size);
        Assert.AreEqual(Vector2.Zero, state.EndPoint);
        Assert.AreEqual(string.Empty, state.Text);
        Assert.AreEqual(string.Empty, state.FontFamily);
    }

    private static void AssertRectangleState(RectangleDrawable shape, ModifyShapeOperation.ShapeState state)
    {
        Assert.AreEqual(state.Offset, shape.Offset);
        Assert.AreEqual(state.Size, shape.Size);
        Assert.AreEqual(state.StrokeColor, shape.StrokeColor);
        Assert.AreEqual(state.FillColor, shape.FillColor);
        Assert.AreEqual(state.StrokeWidth, shape.StrokeWidth);
    }

    private static void AssertTextState(TextDrawable shape, ModifyShapeOperation.ShapeState state)
    {
        Assert.AreEqual(state.Offset, shape.Offset);
        Assert.AreEqual(state.Size, shape.Size);
        Assert.AreEqual(state.Text, shape.Text);
        Assert.AreEqual(state.TextColor, shape.Color);
        Assert.AreEqual(state.TextBackgroundColor, shape.BackgroundColor);
        Assert.AreEqual(state.FontFamily, shape.FontFamily);
        Assert.AreEqual(state.FontSize, shape.FontSize);
    }

    private sealed class UnknownDrawable : IDrawable
    {
        public Vector2 Offset { get; set; }
    }
}
