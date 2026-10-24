using System.Buffers.Binary;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace HRS.Infrastructure.Compiled;

/// <summary>FR-ADM-001's logo at the top of the Word export — PNG only; an SVG logo is left out
/// of Word documents (the name still shows) since Word's SVG support needs a PNG fallback anyway.</summary>
internal static class WordLogo
{
    private const long EmuPerPixel = 9525;
    private const long HeightPixels = 48;

    public static W.Paragraph Paragraph(MainDocumentPart main, byte[] png)
    {
        var imagePart = main.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(png))
        {
            imagePart.FeedData(stream);
        }

        var (width, height) = PngSize(png);
        var cy = HeightPixels * EmuPerPixel;
        var cx = height == 0 ? cy : cy * width / height;
        var relationshipId = main.GetIdOfPart(imagePart);

        var inline = new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.DocProperties { Id = 1U, Name = "Logo" },
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U };

        return new W.Paragraph(new W.Run(new W.Drawing(inline)));
    }

    /// <summary>A PNG's pixel size from its IHDR chunk (big-endian width and height at bytes 16–23).</summary>
    private static (long Width, long Height) PngSize(byte[] png) =>
        png.Length >= 24
            ? (BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)))
            : (1, 1);
}
