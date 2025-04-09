namespace CaptureTool.Capture.Desktop.Annotation;

public sealed partial class ImageAnnotationItem : AnnotationItem
{ 
    public ImageFile ImageFile { get; set; }

    public ImageAnnotationItem(ImageFile imageFile, int left, int top) : base(left, top)
    {
        ImageFile = imageFile;
    }
}

