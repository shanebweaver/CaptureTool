using CaptureTool.Domain.Edit;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.Edit.Operations;
using CaptureTool.Domain.FileSystem;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class ImageEditSessionTests
{
    [TestMethod]
    public void Constructor_ShouldUseFullImageCropAndCopyInitialDrawables()
    {
        var drawable = new RectangleDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 3);
        var initialDrawables = new List<IDrawable> { drawable };

        var session = new ImageEditSession(
            new Size(100, 200),
            ImageOrientation.Rotate90FlipNone,
            new Rectangle(10, 20, 30, 40),
            initialDrawables);
        initialDrawables.Clear();

        Assert.AreEqual(new Size(100, 200), session.ImageSize);
        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, session.Orientation);
        Assert.AreEqual(new Rectangle(10, 20, 30, 40), session.CropRect);
        Assert.HasCount(1, session.Drawables);
        Assert.AreSame(drawable, session.Drawables[0]);

        var defaultSession = new ImageEditSession(new Size(320, 240));
        Assert.AreEqual(new Rectangle(0, 0, 320, 240), defaultSession.CropRect);
    }

    [TestMethod]
    public void CreateRenderSnapshot_ShouldReturnCurrentRenderState()
    {
        var session = new ImageEditSession(
            new Size(100, 200),
            ImageOrientation.Rotate180FlipX,
            new Rectangle(5, 6, 70, 80));

        ImageEditRenderSnapshot snapshot = session.CreateRenderSnapshot();

        Assert.AreEqual(ImageOrientation.Rotate180FlipX, snapshot.Orientation);
        Assert.AreEqual(new Size(100, 200), snapshot.ImageSize);
        Assert.AreEqual(new Rectangle(5, 6, 70, 80), snapshot.CropRect);
    }

    [TestMethod]
    public void DrawableCollectionMethods_ShouldValidateInputsAndIndexes()
    {
        var session = new ImageEditSession(new Size(100, 100));
        var first = new RectangleDrawable(Vector2.Zero, new Size(10, 10), Color.Red, Color.Blue, 1);
        var second = new EllipseDrawable(Vector2.One, new Size(20, 20), Color.Green, Color.Yellow, 2);

        Assert.ThrowsExactly<ArgumentNullException>(() => session.AddDrawable(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => session.InsertDrawable(0, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => session.RemoveDrawable(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.InsertDrawable(-1, first));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.InsertDrawable(1, first));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.GetDrawableAt(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.RemoveDrawableAt(0));

        session.AddDrawable(first);
        session.InsertDrawable(1, second);

        Assert.AreSame(first, session.GetDrawableAt(0));
        Assert.AreSame(second, session.GetDrawableAt(1));
        Assert.IsTrue(session.RemoveDrawable(first));
        Assert.IsFalse(session.RemoveDrawable(first));
        Assert.AreSame(second, session.RemoveDrawableAt(0));
        Assert.IsEmpty(session.Drawables);
    }

    [TestMethod]
    public void ApplyShapeState_ShouldUpdateEllipseLineArrowAndTextDrawables()
    {
        var ellipse = new EllipseDrawable(Vector2.Zero, new Size(1, 2), Color.Red, Color.Blue, 1);
        var line = new LineDrawable(Vector2.Zero, Vector2.One, Color.Red, 1);
        var arrow = new ArrowDrawable(Vector2.One, new Vector2(2), Color.Blue, 2);
        var text = new TextDrawable(Vector2.Zero, "old", Color.Black);
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 100),
            [ellipse, line, arrow, text]);
        var state = new ModifyShapeOperation.ShapeState
        {
            Offset = new Vector2(10, 20),
            Size = new Size(30, 40),
            EndPoint = new Vector2(50, 60),
            StrokeColor = Color.Green,
            FillColor = Color.Yellow,
            StrokeWidth = 7,
            Text = "updated",
            TextColor = Color.White,
            TextBackgroundColor = Color.DarkGray,
            FontFamily = "Consolas",
            FontSize = 19,
        };

        session.ApplyShapeState(0, state);
        session.ApplyShapeState(1, state);
        session.ApplyShapeState(2, state);
        session.ApplyShapeState(3, state);

        Assert.AreEqual(state.Offset, ellipse.Offset);
        Assert.AreEqual(state.Size, ellipse.Size);
        Assert.AreEqual(state.StrokeColor, ellipse.StrokeColor);
        Assert.AreEqual(state.FillColor, ellipse.FillColor);
        Assert.AreEqual(state.StrokeWidth, ellipse.StrokeWidth);

        Assert.AreEqual(state.Offset, line.Offset);
        Assert.AreEqual(state.EndPoint, line.EndPoint);
        Assert.AreEqual(state.StrokeColor, line.StrokeColor);
        Assert.AreEqual(state.StrokeWidth, line.StrokeWidth);

        Assert.AreEqual(state.Offset, arrow.Offset);
        Assert.AreEqual(state.EndPoint, arrow.EndPoint);
        Assert.AreEqual(state.StrokeColor, arrow.StrokeColor);
        Assert.AreEqual(state.StrokeWidth, arrow.StrokeWidth);

        Assert.AreEqual(state.Offset, text.Offset);
        Assert.AreEqual(state.Size, text.Size);
        Assert.AreEqual(state.Text, text.Text);
        Assert.AreEqual(state.TextColor, text.Color);
        Assert.AreEqual(state.TextBackgroundColor, text.BackgroundColor);
        Assert.AreEqual(state.FontFamily, text.FontFamily);
        Assert.AreEqual(state.FontSize, text.FontSize);
    }

    [TestMethod]
    public void SetChromaKeySettings_ShouldHandleSessionsWithoutImageDrawable()
    {
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 100),
            [new RectangleDrawable(Vector2.Zero, new Size(10, 10), Color.Red, Color.Blue, 1)]);
        var settings = new ChromaKeySettings(1, Color.Green, 40, 20);

        session.SetChromaKeySettings(settings);

        Assert.AreEqual(settings, session.ChromaKeySettings);
    }

    [TestMethod]
    public void SetChromaKeySettings_ShouldUpdateExistingImageEffect()
    {
        var effect = new ImageChromaKeyEffect(Color.Red, 0.1f, 0.2f);
        var image = new ImageDrawable(Vector2.Zero, new ImageFile("image.png"), new Size(100, 100))
        {
            ImageEffect = effect,
        };
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 100),
            [image]);
        var settings = new ChromaKeySettings(1, Color.Empty, 90, 30);

        session.SetChromaKeySettings(settings);

        Assert.AreSame(effect, image.ImageEffect);
        Assert.AreEqual(Color.Empty, effect.Color);
        Assert.AreEqual(0.9f, effect.Tolerance);
        Assert.AreEqual(0.3f, effect.Desaturation);
        Assert.IsFalse(effect.IsEnabled);
    }

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

        Assert.HasCount(1, session.Drawables);
        Assert.AreSame(drawable, session.Drawables[0]);

        history.Execute(session, new DeleteDrawableCommand(0));

        Assert.IsFalse(session.Drawables.Any());

        Assert.IsTrue(history.Undo(session));
        Assert.HasCount(1, session.Drawables);
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

        Assert.IsInstanceOfType<ImageChromaKeyEffect>(image.ImageEffect);
        var effect = (ImageChromaKeyEffect)image.ImageEffect!;
        Assert.IsTrue(effect.IsEnabled);
        Assert.AreEqual(Color.Green, effect.Color);
        Assert.AreEqual(0.5f, effect.Tolerance);
        Assert.AreEqual(0.25f, effect.Desaturation);

        Assert.IsTrue(history.Undo(session));

        Assert.IsInstanceOfType<ImageChromaKeyEffect>(image.ImageEffect);
        effect = (ImageChromaKeyEffect)image.ImageEffect!;
        Assert.IsFalse(effect.IsEnabled);
        Assert.AreEqual(Color.Empty, effect.Color);
        Assert.AreEqual(0.3f, effect.Tolerance);
        Assert.AreEqual(0f, effect.Desaturation);
    }
}
