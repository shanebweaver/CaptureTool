using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class ImageEditSessionTests
{
    [TestMethod]
    public void History_ShouldUndoAndRedoRotation()
    {
        var session = new ImageEditSession(new Size(100, 200));
        var history = new ImageEditHistory();

        history.Execute(session, new RotateImageCommand(RotationDirection.Clockwise));

        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, session.Orientation);
        Assert.AreEqual(new Rectangle(0, 0, 200, 100), session.CropRect);
        Assert.IsTrue(history.CanUndo);
        Assert.IsFalse(history.CanRedo);

        Assert.IsTrue(history.Undo(session));

        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, session.Orientation);
        Assert.AreEqual(new Rectangle(0, 0, 100, 200), session.CropRect);
        Assert.IsFalse(history.CanUndo);
        Assert.IsTrue(history.CanRedo);

        Assert.IsTrue(history.Redo(session));

        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, session.Orientation);
        Assert.AreEqual(new Rectangle(0, 0, 200, 100), session.CropRect);
    }

    [TestMethod]
    public void History_ShouldUndoAndRedoDrawableChanges()
    {
        var session = new ImageEditSession(new Size(100, 100));
        var history = new ImageEditHistory();
        var drawable = new RectangleDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 3);

        history.Execute(session, new AddDrawableCommand(drawable));

        Assert.IsTrue(session.Drawables.Count == 1);
        Assert.AreSame(drawable, session.Drawables[0]);

        history.Execute(session, new DeleteDrawableCommand(0));

        Assert.IsFalse(session.Drawables.Any());

        Assert.IsTrue(history.Undo(session));
        Assert.IsTrue(session.Drawables.Count == 1);
        Assert.AreSame(drawable, session.Drawables[0]);

        Assert.IsTrue(history.Redo(session));
        Assert.IsFalse(session.Drawables.Any());
    }

    [TestMethod]
    public void ModifyDrawableCommand_ShouldRestoreShapeState()
    {
        var rectangle = new RectangleDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 3);
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 100),
            [rectangle]);
        var history = new ImageEditHistory();
        var oldState = new ModifyShapeOperation.ShapeState(rectangle);
        var newState = new ModifyShapeOperation.ShapeState
        {
            Offset = new Vector2(20, 30),
            Size = new Size(40, 50),
            EndPoint = default,
            StrokeColor = Color.Green,
            FillColor = Color.Yellow,
            StrokeWidth = 7,
            Text = string.Empty,
            TextColor = default,
            TextBackgroundColor = default,
            FontFamily = string.Empty,
            FontSize = default,
        };

        history.Execute(session, new ModifyDrawableCommand(0, oldState, newState));

        Assert.AreEqual(new Vector2(20, 30), rectangle.Offset);
        Assert.AreEqual(new Size(40, 50), rectangle.Size);
        Assert.AreEqual(Color.Green, rectangle.StrokeColor);
        Assert.AreEqual(Color.Yellow, rectangle.FillColor);
        Assert.AreEqual(7, rectangle.StrokeWidth);

        Assert.IsTrue(history.Undo(session));

        Assert.AreEqual(new Vector2(1, 2), rectangle.Offset);
        Assert.AreEqual(new Size(10, 20), rectangle.Size);
        Assert.AreEqual(Color.Red, rectangle.StrokeColor);
        Assert.AreEqual(Color.Blue, rectangle.FillColor);
        Assert.AreEqual(3, rectangle.StrokeWidth);
    }

    [TestMethod]
    public void SetChromaKeyCommand_ShouldUpdateImageEffectAndUndo()
    {
        var image = new ImageDrawable(Vector2.Zero, new ImageFile("image.png"), new Size(100, 100));
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 100),
            [image]);
        var history = new ImageEditHistory();
        var settings = new ChromaKeySettings(1, Color.Green, 50, 25);

        history.Execute(session, new SetChromaKeyCommand(ChromaKeySettings.Default, settings));

        Assert.IsInstanceOfType(image.ImageEffect, typeof(ImageChromaKeyEffect));
        var effect = (ImageChromaKeyEffect)image.ImageEffect!;
        Assert.IsTrue(effect.IsEnabled);
        Assert.AreEqual(Color.Green, effect.Color);
        Assert.AreEqual(0.5f, effect.Tolerance);
        Assert.AreEqual(0.25f, effect.Desaturation);

        Assert.IsTrue(history.Undo(session));

        Assert.IsInstanceOfType(image.ImageEffect, typeof(ImageChromaKeyEffect));
        effect = (ImageChromaKeyEffect)image.ImageEffect!;
        Assert.IsFalse(effect.IsEnabled);
        Assert.AreEqual(Color.Empty, effect.Color);
        Assert.AreEqual(0.3f, effect.Tolerance);
        Assert.AreEqual(0f, effect.Desaturation);
    }
}
