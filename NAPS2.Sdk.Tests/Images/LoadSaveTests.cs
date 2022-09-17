using System.Globalization;
using NAPS2.Sdk.Tests.Asserts;
using Xunit;

namespace NAPS2.Sdk.Tests.Images;

public class LoadSaveTests : ContextualTests
{
    // TODO: Add tests for error/edge cases (e.g. invalid files, mismatched extensions/format (?), tiff progress, saving 0/null images, unicode file names)
    // TODO: Verify rough expected file sizes to ensure compression is as expected (especially for tiff saving)

    [Theory]
    [MemberData(nameof(TestCases))]
    public void LoadFromFile(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var path = CopyResourceToFile(GetResource(resource), $"image{ext}");
        using var image = ImageContext.Load(path);
        Assert.Equal(format, image.OriginalFileFormat);
        ImageAsserts.Similar(GetResource(compare[0]), image);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void LoadFromStream(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var stream = new MemoryStream(GetResource(resource));
        using var image = ImageContext.Load(stream);
        Assert.Equal(format, image.OriginalFileFormat);
        ImageAsserts.Similar(GetResource(compare[0]), image);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void LoadFramesFromFile(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var path = CopyResourceToFile(GetResource(resource), $"image{ext}");
        var images = ImageContext.LoadFrames(path, out var count).ToArray();
        Assert.Equal(compare.Length, count);
        Assert.Equal(compare.Length, images.Length);
        for (int i = 0; i < images.Length; i++)
        {
            Assert.Equal(format, images[i].OriginalFileFormat);
            ImageAsserts.Similar(GetResource(compare[i]), images[i]);
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void LoadFramesFromStream(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var stream = new MemoryStream(GetResource(resource));
        var images = ImageContext.LoadFrames(stream, out var count).ToArray();
        Assert.Equal(compare.Length, count);
        Assert.Equal(compare.Length, images.Length);
        for (int i = 0; i < images.Length; i++)
        {
            Assert.Equal(format, images[i].OriginalFileFormat);
            ImageAsserts.Similar(GetResource(compare[i]), images[i]);
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SaveToFile(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var image = LoadImage(GetResource(resource));
        var path = Path.Combine(FolderPath, $"image{ext}");
        image.Save(path);
        var image2 = ImageContext.Load(path);
        Assert.Equal(format, image2.OriginalFileFormat);
        ImageAsserts.Similar(GetResource(compare[0]), image2);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void SaveToStream(ImageFileFormat format, string ext, string resource, string[] compare)
    {
        var image = LoadImage(GetResource(resource));
        var stream = new MemoryStream();
        image.Save(stream, format);
        var image2 = ImageContext.Load(stream);
        Assert.Equal(format, image2.OriginalFileFormat);
        ImageAsserts.Similar(GetResource(compare[0]), image2);
    }

    [Fact]
    public void SaveSinglePageTiffToFile()
    {
        var path = Path.Combine(FolderPath, "image.tiff");
        var original = LoadImage(ImageResources.color_image);

        ImageContext.TiffWriter.SaveTiff(new[] { original }, path);
        AssertTiff(path, ImageResources.color_image);
    }

    [Fact]
    public void SaveMultiPageTiffToFile()
    {
        var path = Path.Combine(FolderPath, "image.tiff");
        var original = new[]
        {
            LoadImage(ImageResources.color_image),
            LoadImage(ImageResources.color_image_bw).PerformTransform(new BlackWhiteTransform()),
            LoadImage(ImageResources.stock_cat)
        };

        ImageContext.TiffWriter.SaveTiff(original, path);
        AssertTiff(path, ImageResources.color_image, ImageResources.color_image_bw, ImageResources.stock_cat);
    }

    [Fact]
    public void SaveSinglePageTiffToStream()
    {
        var stream = new MemoryStream();
        var original = LoadImage(ImageResources.color_image);

        ImageContext.TiffWriter.SaveTiff(new[] { original }, stream);
        AssertTiff(stream, ImageResources.color_image);
    }

    [Fact]
    public void SaveMultiPageTiffToStream()
    {
        var stream = new MemoryStream();
        var original = new[]
        {
            LoadImage(ImageResources.color_image),
            LoadImage(ImageResources.color_image_bw).PerformTransform(new BlackWhiteTransform()),
            LoadImage(ImageResources.stock_cat)
        };

        ImageContext.TiffWriter.SaveTiff(original, stream);
        AssertTiff(stream, ImageResources.color_image, ImageResources.color_image_bw, ImageResources.stock_cat);
    }

    [Fact]
    public void SaveBlackAndWhiteTiff()
    {
        var path = Path.Combine(FolderPath, "image.tiff");
        var original = LoadImage(ImageResources.color_image_bw);
        original = original.PerformTransform(new BlackWhiteTransform());

        ImageContext.TiffWriter.SaveTiff(new[] { original }, path);
        AssertTiff(path, ImageResources.color_image_bw);
    }

    [Fact]
    public void SaveColorTiffWithG4()
    {
        var path = Path.Combine(FolderPath, "image.tiff");
        var original = LoadImage(ImageResources.color_image);

        ImageContext.TiffWriter.SaveTiff(new[] { original }, path, TiffCompressionType.Ccitt4);
        AssertTiff(path, ImageResources.color_image_bw);
    }

    private void AssertTiff(string path, params byte[][] expectedImages)
    {
        var actual = ImageContext.LoadFrames(path, out var count).ToArray();
        DoAssertTiff(actual, count, expectedImages);
    }

    private void AssertTiff(Stream stream, params byte[][] expectedImages)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var actual = ImageContext.LoadFrames(stream, out var count).ToArray();
        DoAssertTiff(actual, count, expectedImages);
    }

    private static void DoAssertTiff(IMemoryImage[] actual, int count, byte[][] expectedImages)
    {
        Assert.Equal(expectedImages.Length, count);
        Assert.Equal(expectedImages.Length, actual.Length);
        for (int i = 0; i < expectedImages.Length; i++)
        {
            Assert.Equal(ImageFileFormat.Tiff, actual[i].OriginalFileFormat);
            ImageAsserts.Similar(expectedImages[i], actual[i]);
        }
    }

    private static byte[] GetResource(string resource) =>
        (byte[]) ImageResources.ResourceManager.GetObject(resource, CultureInfo.InvariantCulture);

    public static IEnumerable<object[]> TestCases = new List<object[]>
    {
        new object[]
        {
            ImageFileFormat.Png, ".png", "color_image_png",
            new[] { "color_image" }
        },
        new object[]
        {
            ImageFileFormat.Jpeg, ".jpg", "color_image",
            new[] { "color_image" }
        },
        new object[]
        {
            ImageFileFormat.Bmp, ".bmp", "color_image_bw_invertpal",
            new[] { "color_image_bw" }
        },
        new object[]
        {
            ImageFileFormat.Tiff, ".tiff", "color_image_tiff",
            new[] { "color_image", "color_image_h_p300", "stock_cat" }
        },
    };
}