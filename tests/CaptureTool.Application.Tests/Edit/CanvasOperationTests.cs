using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class CanvasOperationTests
{
    [TestMethod]
    public void AddShapeOperation_RedoAndUndo_AddsAndRemovesShape()
    {
        var drawables = new ObservableCollection<IDrawable>();
        var shape = new RectangleDrawable(new Vector2(1, 2), new Size(3, 4), Color.Red, Color.Blue, 5);
        int invalidations = 0;
        var operation = new AddShapeOperation(drawables, shape, () => invalidations++);

        operation.Redo();
        operation.Undo();

        Assert.IsEmpty(drawables);
        Assert.AreEqual(2, invalidations);
    }

    [TestMethod]
    public void DeleteShapeOperation_RedoAndUndo_RemovesAndRestoresOriginalPosition()
    {
        var first = new RectangleDrawable(new Vector2(1, 1), new Size(10, 10), Color.Black, Color.White, 1);
        var deleted = new EllipseDrawable(new Vector2(2, 2), new Size(20, 20), Color.Red, Color.Blue, 2);
        var last = new LineDrawable(new Vector2(3, 3), new Vector2(30, 30), Color.Green, 3);
        var drawables = new ObservableCollection<IDrawable> { first, deleted, last };
        int invalidations = 0;
        var operation = new DeleteShapeOperation(drawables, deleted, 1, () => invalidations++);

        operation.Redo();
        operation.Undo();

        CollectionAssert.AreEqual(new IDrawable[] { first, deleted, last }, drawables);
        Assert.AreEqual(2, invalidations);
    }

    [TestMethod]
    public void CropOperation_RedoAndUndo_AppliesNewAndOldRectangles()
    {
        var applied = new List<Rectangle>();
        var oldRectangle = new Rectangle(1, 2, 3, 4);
        var newRectangle = new Rectangle(5, 6, 7, 8);
        var operation = new CropOperation(applied.Add, oldRectangle, newRectangle);

        operation.Redo();
        operation.Undo();

        CollectionAssert.AreEqual(new[] { newRectangle, oldRectangle }, applied);
    }

    [TestMethod]
    public void OrientationOperation_RedoAndUndo_AppliesNewAndOldOrientations()
    {
        var applied = new List<ImageOrientation>();
        var operation = new OrientationOperation(
            applied.Add,
            ImageOrientation.RotateNoneFlipNone,
            ImageOrientation.Rotate90FlipX);

        operation.Redo();
        operation.Undo();

        CollectionAssert.AreEqual(
            new[] { ImageOrientation.Rotate90FlipX, ImageOrientation.RotateNoneFlipNone },
            applied);
    }

    [TestMethod]
    public void ChromaKeyOperation_RedoAndUndo_AppliesNewAndOldStates()
    {
        var applied = new List<ChromaKeyOperation.ChromaKeyState>();
        var oldState = new ChromaKeyOperation.ChromaKeyState(0, Color.Green, 10, 20);
        var newState = new ChromaKeyOperation.ChromaKeyState(1, Color.Blue, 30, 40);
        var operation = new ChromaKeyOperation(applied.Add, oldState, newState);

        operation.Redo();
        operation.Undo();

        CollectionAssert.AreEqual(new[] { newState, oldState }, applied);
    }
}
