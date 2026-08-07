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
    public void Constructor_ShouldClampCropRectToOrientedImageBounds()
    {
        var session = new ImageEditSession(
            new Size(100, 200),
            ImageOrientation.Rotate90FlipNone,
            new Rectangle(180, 90, 50, 50));

        Assert.AreEqual(new Rectangle(150, 50, 50, 50), session.CropRect);
    }

    [TestMethod]
    public void SetCropRect_ShouldClampCropRectToImageBoundsWithMinimumSize()
    {
        var session = new ImageEditSession(new Size(100, 80));

        session.SetCropRect(new Rectangle(-10, 90, 200, 0));

        Assert.AreEqual(new Rectangle(0, 79, 100, 1), session.CropRect);
    }

    [TestMethod]
    public void ResizeImage_ShouldScaleCropAndEveryDrawableType()
    {
        var image = new ImageDrawable(new Vector2(1, 2), new ImageFile("image.png"), new Size(10, 20));
        var rectangle = new RectangleDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 2);
        var ellipse = new EllipseDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 2);
        var line = new LineDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 2);
        var arrow = new ArrowDrawable(new Vector2(1, 2), new Vector2(3, 4), Color.Red, 2);
        var text = new TextDrawable(
            new Vector2(1, 2),
            new Size(10, 20),
            "text",
            Color.Black,
            Color.White,
            "Segoe UI",
            10);
        var session = new ImageEditSession(
            new Size(100, 100),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(10, 20, 30, 40),
            [image, rectangle, ellipse, line, arrow, text]);

        session.ResizeImage(new Size(200, 300));

        Assert.AreEqual(new Rectangle(20, 60, 60, 120), session.CropRect);
        Assert.AreEqual(new Vector2(2, 6), image.Offset);
        Assert.AreEqual(new Size(20, 60), image.ImageSize);
        Assert.AreEqual(new Vector2(2, 6), rectangle.Offset);
        Assert.AreEqual(new Size(20, 60), rectangle.Size);
        Assert.AreEqual(5, rectangle.StrokeWidth);
        Assert.AreEqual(new Vector2(2, 6), ellipse.Offset);
        Assert.AreEqual(new Size(20, 60), ellipse.Size);
        Assert.AreEqual(5, ellipse.StrokeWidth);
        Assert.AreEqual(new Vector2(2, 6), line.Offset);
        Assert.AreEqual(new Vector2(6, 12), line.EndPoint);
        Assert.AreEqual(5, line.StrokeWidth);
        Assert.AreEqual(new Vector2(2, 6), arrow.Offset);
        Assert.AreEqual(new Vector2(6, 12), arrow.EndPoint);
        Assert.AreEqual(5, arrow.StrokeWidth);
        Assert.AreEqual(new Vector2(2, 6), text.Offset);
        Assert.AreEqual(new Size(20, 60), text.Size);
        Assert.AreEqual(25, text.FontSize);
    }

    [TestMethod]
    public void ResizeAndOrientation_WhenValuesDoNotRequireScaling_HandleNoOpAndEmptyImage()
    {
        var emptySession = new ImageEditSession(Size.Empty);

        emptySession.ResizeImage(new Size(30, 40));
        emptySession.ResizeImage(new Size(30, 40));
        emptySession.SetOrientation(emptySession.Orientation);

        Assert.AreEqual(new Size(30, 40), emptySession.ImageSize);
        Assert.AreEqual(new Rectangle(0, 0, 30, 40), emptySession.CropRect);
    }

    [TestMethod]
    public void SetAndFlipCommands_ShouldApplyAndRevertState()
    {
        var session = new ImageEditSession(new Size(100, 80));
        var setCrop = new SetCropCommand(
            new Rectangle(0, 0, 100, 80),
            new Rectangle(10, 20, 30, 40));
        var setOrientation = new SetOrientationCommand(
            ImageOrientation.RotateNoneFlipNone,
            ImageOrientation.Rotate90FlipNone);
        var flip = new FlipImageCommand(FlipDirection.Horizontal);

        setCrop.Apply(session);
        Assert.AreEqual(new Rectangle(10, 20, 30, 40), session.CropRect);
        setCrop.Revert(session);
        Assert.AreEqual(new Rectangle(0, 0, 100, 80), session.CropRect);

        setOrientation.Apply(session);
        Assert.AreEqual(ImageOrientation.Rotate90FlipNone, session.Orientation);
        setOrientation.Revert(session);
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, session.Orientation);

        flip.Apply(session);
        ImageOrientation flipped = session.Orientation;
        Assert.AreNotEqual(ImageOrientation.RotateNoneFlipNone, flipped);
        flip.Revert(session);
        Assert.AreEqual(ImageOrientation.RotateNoneFlipNone, session.Orientation);
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
        var state = new ShapeState
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
    public void History_ShouldRebaseCropAcrossResolutionChanges()
    {
        var session = new ImageEditSession(new Size(100, 50));
        var history = new ImageEditHistory();
        history.Execute(
            session,
            new SetCropCommand(
                new Rectangle(0, 0, 100, 50),
                new Rectangle(10, 5, 60, 30)));

        session.ResizeImage(new Size(200, 100));

        Assert.IsTrue(history.Undo(session));
        Assert.AreEqual(new Rectangle(0, 0, 200, 100), session.CropRect);

        Assert.IsTrue(history.Redo(session));
        Assert.AreEqual(new Rectangle(20, 10, 120, 60), session.CropRect);
    }

    [TestMethod]
    public void History_ShouldRebaseEveryModifiedDrawableTypeAcrossResolutionChanges()
    {
        var rectangle = new RectangleDrawable(new Vector2(1, 2), new Size(10, 20), Color.Red, Color.Blue, 2);
        var ellipse = new EllipseDrawable(new Vector2(2, 3), new Size(11, 21), Color.Green, Color.Yellow, 3);
        var line = new LineDrawable(new Vector2(3, 4), new Vector2(13, 14), Color.Purple, 4);
        var arrow = new ArrowDrawable(new Vector2(4, 5), new Vector2(14, 15), Color.Orange, 5);
        var text = new TextDrawable(new Vector2(5, 6), new Size(15, 25), "old", Color.Black, Color.White, "Segoe UI", 10);
        var session = new ImageEditSession(
            new Size(100, 50),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 50),
            [rectangle, ellipse, line, arrow, text]);
        var history = new ImageEditHistory();
        IDrawable[] replacements =
        [
            new RectangleDrawable(new Vector2(11, 12), new Size(20, 30), Color.Aqua, Color.Beige, 6),
            new EllipseDrawable(new Vector2(12, 13), new Size(21, 31), Color.Brown, Color.Coral, 7),
            new LineDrawable(new Vector2(13, 14), new Vector2(23, 24), Color.Cyan, 8),
            new ArrowDrawable(new Vector2(14, 15), new Vector2(24, 25), Color.Gold, 9),
            new TextDrawable(new Vector2(15, 16), new Size(25, 35), "new", Color.Navy, Color.Silver, "Consolas", 20),
        ];

        for (int i = 0; i < replacements.Length; i++)
        {
            history.Execute(
                session,
                new ModifyDrawableCommand(
                    i,
                    new ShapeState(session.Drawables[i]),
                    new ShapeState(replacements[i])));
        }

        session.ResizeImage(new Size(200, 100));

        for (int i = replacements.Length - 1; i >= 0; i--)
        {
            Assert.IsTrue(history.Undo(session));
        }

        Assert.AreEqual(new Vector2(2, 4), rectangle.Offset);
        Assert.AreEqual(new Size(20, 40), rectangle.Size);
        Assert.AreEqual(4, rectangle.StrokeWidth);
        Assert.AreEqual(new Vector2(4, 6), ellipse.Offset);
        Assert.AreEqual(new Size(22, 42), ellipse.Size);
        Assert.AreEqual(6, ellipse.StrokeWidth);
        Assert.AreEqual(new Vector2(6, 8), line.Offset);
        Assert.AreEqual(new Vector2(26, 28), line.EndPoint);
        Assert.AreEqual(8, line.StrokeWidth);
        Assert.AreEqual(new Vector2(8, 10), arrow.Offset);
        Assert.AreEqual(new Vector2(28, 30), arrow.EndPoint);
        Assert.AreEqual(10, arrow.StrokeWidth);
        Assert.AreEqual(new Vector2(10, 12), text.Offset);
        Assert.AreEqual(new Size(30, 50), text.Size);
        Assert.AreEqual(20, text.FontSize);

        for (int i = 0; i < replacements.Length; i++)
        {
            Assert.IsTrue(history.Redo(session));
        }

        Assert.AreEqual(new Vector2(22, 24), rectangle.Offset);
        Assert.AreEqual(new Size(40, 60), rectangle.Size);
        Assert.AreEqual(new Vector2(24, 26), ellipse.Offset);
        Assert.AreEqual(new Size(42, 62), ellipse.Size);
        Assert.AreEqual(new Vector2(26, 28), line.Offset);
        Assert.AreEqual(new Vector2(46, 48), line.EndPoint);
        Assert.AreEqual(new Vector2(28, 30), arrow.Offset);
        Assert.AreEqual(new Vector2(48, 50), arrow.EndPoint);
        Assert.AreEqual(new Vector2(30, 32), text.Offset);
        Assert.AreEqual(new Size(50, 70), text.Size);
        Assert.AreEqual(40, text.FontSize);
    }

    [TestMethod]
    public void History_ShouldRebaseDetachedDrawablesWithoutDoubleScalingLiveDrawables()
    {
        var added = new RectangleDrawable(new Vector2(2, 3), new Size(10, 20), Color.Red, Color.Blue, 2);
        var addSession = new ImageEditSession(new Size(100, 50));
        var addHistory = new ImageEditHistory();
        addHistory.Execute(addSession, new AddDrawableCommand(added));
        Assert.IsTrue(addHistory.Undo(addSession));

        addSession.ResizeImage(new Size(200, 100));
        Assert.IsTrue(addHistory.Redo(addSession));

        Assert.AreEqual(new Vector2(4, 6), added.Offset);
        Assert.AreEqual(new Size(20, 40), added.Size);
        Assert.AreEqual(4, added.StrokeWidth);

        var deleted = new RectangleDrawable(new Vector2(4, 5), new Size(15, 25), Color.Green, Color.Yellow, 3);
        var deleteSession = new ImageEditSession(
            new Size(100, 50),
            ImageOrientation.RotateNoneFlipNone,
            new Rectangle(0, 0, 100, 50),
            [deleted]);
        var deleteHistory = new ImageEditHistory();
        deleteHistory.Execute(deleteSession, new DeleteDrawableCommand(0));

        deleteSession.ResizeImage(new Size(200, 100));
        Assert.IsTrue(deleteHistory.Undo(deleteSession));

        Assert.AreEqual(new Vector2(8, 10), deleted.Offset);
        Assert.AreEqual(new Size(30, 50), deleted.Size);
        Assert.AreEqual(6, deleted.StrokeWidth);
    }

    [TestMethod]
    public void History_ShouldRebaseEntriesRecordedAtDifferentResolutionsIndependently()
    {
        var session = new ImageEditSession(new Size(100, 50));
        var history = new ImageEditHistory();
        history.Execute(
            session,
            new SetCropCommand(new Rectangle(0, 0, 100, 50), new Rectangle(10, 5, 80, 40)));

        session.ResizeImage(new Size(200, 100));
        history.Execute(
            session,
            new SetCropCommand(new Rectangle(20, 10, 160, 80), new Rectangle(40, 20, 120, 60)));
        session.ResizeImage(new Size(100, 50));

        Assert.IsTrue(history.Undo(session));
        Assert.AreEqual(new Rectangle(10, 5, 80, 40), session.CropRect);

        Assert.IsTrue(history.Undo(session));
        Assert.AreEqual(new Rectangle(0, 0, 100, 50), session.CropRect);
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
        var oldState = new ShapeState(rectangle);
        var newState = new ShapeState
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
