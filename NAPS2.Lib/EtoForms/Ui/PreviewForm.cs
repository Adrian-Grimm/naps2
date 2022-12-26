using Eto.Drawing;
using Eto.Forms;

namespace NAPS2.EtoForms.Ui;

public class PreviewForm : EtoDialogBase
{
    private readonly DesktopCommands _desktopCommands;
    private readonly IIconProvider _iconProvider;
    private readonly KeyboardShortcutManager _ksm;

    private readonly Scrollable _scrollable = new() { Border = BorderType.None };
    private readonly Drawable _imageView = new() { BackgroundColor = Colors.White };
    private UiImage? _currentImage;
    private Bitmap? _renderedImage;
    private Size _renderSize;

    public PreviewForm(Naps2Config config, DesktopCommands desktopCommands, UiImageList imageList,
        IIconProvider iconProvider, KeyboardShortcutManager ksm) : base(config)
    {
        _desktopCommands = desktopCommands;
        ImageList = imageList;
        _iconProvider = iconProvider;
        _ksm = ksm;

        _scrollable.Content = _imageView;
        _imageView.Paint += ImagePaint;

        GoToPrevCommand = new ActionCommand(() => GoTo(ImageIndex - 1))
        {
            Text = UiStrings.Previous,
            Image = iconProvider.GetIcon("arrow_left")
        };
        GoToNextCommand = new ActionCommand(() => GoTo(ImageIndex + 1))
        {
            Text = UiStrings.Next,
            Image = iconProvider.GetIcon("arrow_right")
        };
        ZoomInCommand = new ActionCommand(() => Zoom(1))
        {
            Text = UiStrings.ZoomIn,
            Image = iconProvider.GetIcon("zoom_in")
        };
        ZoomOutCommand = new ActionCommand(() => Zoom(-1))
        {
            Text = UiStrings.ZoomOut,
            Image = iconProvider.GetIcon("zoom_out")
        };
        ZoomWindowCommand = new ActionCommand(ZoomToWindow)
        {
            Text = UiStrings.ZoomActual,
            Image = iconProvider.GetIcon("zoom_actual")
        };
        ZoomActualCommand = new ActionCommand(ZoomToActual)
        {
            Text = UiStrings.ZoomActual,
            Image = iconProvider.GetIcon("zoom_actual")
        };
        DeleteCurrentImageCommand = new ActionCommand(DeleteCurrentImage)
        {
            Text = UiStrings.Delete,
            Image = iconProvider.GetIcon("cross")
        };
    }

    private void ImagePaint(object sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Colors.White);
        if (_renderedImage != null)
        {
            var xOff = (_imageView.Width - RenderSize.Width) / 2;
            var yOff = (_imageView.Height - RenderSize.Height) / 2;
            e.Graphics.DrawRectangle(Colors.Black, xOff - 1, yOff - 1, RenderSize.Width + 1, RenderSize.Height + 1);
            e.Graphics.DrawImage(_renderedImage, xOff, yOff, RenderSize.Width, RenderSize.Height);
        }
    }

    protected override void BuildLayout()
    {
        Title = UiStrings.PreviewFormTitle;
        Icon = Icons.favicon.ToEtoIcon();

        FormStateController.AutoLayoutSize = false;
        FormStateController.DefaultClientSize = new Size(800, 600);

        LayoutController.RootPadding = 0;
        LayoutController.Content = _scrollable;
    }

    protected DesktopCommands Commands { get; set; } = null!;
    protected ActionCommand DeleteCurrentImageCommand { get; }
    protected ActionCommand GoToPrevCommand { get; }
    protected ActionCommand GoToNextCommand { get; }
    protected ActionCommand ZoomInCommand { get; }
    protected ActionCommand ZoomOutCommand { get; }
    protected ActionCommand ZoomWindowCommand { get; }
    protected ActionCommand ZoomActualCommand { get; }

    protected UiImageList ImageList { get; }

    protected Size RenderSize
    {
        get => _renderSize;
        set
        {
            _renderSize = value;
            _imageView.Size = value + new Size(2, 2);
            _imageView.Invalidate();
        }
    }

    public UiImage CurrentImage
    {
        get => _currentImage ?? throw new InvalidOperationException();
        set
        {
            if (_currentImage != null)
            {
                _currentImage.ThumbnailInvalidated -= ImageThumbnailInvalidated;
            }
            _currentImage = value;
            Commands = _desktopCommands.WithSelection(() => ListSelection.Of(_currentImage));
            _currentImage.ThumbnailInvalidated += ImageThumbnailInvalidated;
        }
    }

    private void ImageThumbnailInvalidated(object? sender, EventArgs e)
    {
        Invoker.Current.SafeInvoke(() => UpdateImage().AssertNoAwait());
    }

    protected int ImageIndex
    {
        get
        {
            var index = ImageList!.Images.IndexOf(CurrentImage);
            if (index == -1)
            {
                index = 0;
            }
            return index;
        }
    }

    protected override async void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);
        // TODO: Implement
        // _tbPageCurrent.Visible = PlatformCompat.Runtime.IsToolbarTextboxSupported;
        // if (Config.Get(c => c.HiddenButtons).HasFlag(ToolbarButtons.SavePdf))
        // {
        //     _toolStrip1.Items.Remove(_tsSavePdf);
        // }
        // if (Config.Get(c => c.HiddenButtons).HasFlag(ToolbarButtons.SaveImages))
        // {
        //     _toolStrip1.Items.Remove(_tsSaveImage);
        // }

        // TODO: Implement mouse and keyboard controls
        AssignKeyboardShortcuts();

        // TODO: We should definitely start with separate image forms, but it might be fairly trivial to, when opened
        // from the preview form, have the temporary rendering be propagated back to the viewer form and have the
        // dialog only show the editing controls. So it feels more like an image editor. Clicking e.g. "Crop" in the
        // desktop form should still open the full image editing form (for now at least).
        CreateToolbar();

        UpdatePage();
        await UpdateImage();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ZoomToWindow();
    }

    protected virtual void CreateToolbar()
    {
        ToolBar = new ToolBar
        {
            Items =
            {
                new DropDownToolItem
                {
                    Image = _iconProvider.GetIcon("arrow_rotate_anticlockwise_small"),
                    ToolTip = UiStrings.Rotate,
                    Items =
                    {
                        Commands.RotateLeft,
                        Commands.RotateRight,
                        Commands.Flip,
                        Commands.Deskew,
                        Commands.CustomRotate
                    }
                },
                MakeToolButton(Commands.Crop),
                MakeToolButton(Commands.BrightCont),
                MakeToolButton(Commands.HueSat),
                MakeToolButton(Commands.BlackWhite),
                MakeToolButton(Commands.Sharpen),
                new SeparatorToolItem(),
                MakeToolButton(Commands.SaveSelectedPdf, _iconProvider.GetIcon("file_extension_pdf")),
                MakeToolButton(Commands.SaveSelectedImages, _iconProvider.GetIcon("picture_small")),
                new SeparatorToolItem(),
                MakeToolButton(DeleteCurrentImageCommand),
            }
        };
    }

    private ToolItem MakeToolButton(ActionCommand command, Image? image = null)
    {
        return new ButtonToolItem
        {
            Command = command,
            Image = image ?? command.Image,
            Text = "",
            ToolTip = command.Text
        };
    }

    private async Task GoTo(int index)
    {
        lock (ImageList)
        {
            if (index == ImageIndex || index < 0 || index >= ImageList.Images.Count)
            {
                return;
            }
            CurrentImage = ImageList.Images[index];
            ImageList.UpdateSelection(ListSelection.Of(CurrentImage));
        }
        UpdatePage();
        await UpdateImage();
    }

    protected virtual void UpdatePage()
    {
        // TODO: Implement
        // _tbPageCurrent.Text = (ImageIndex + 1).ToString(CultureInfo.CurrentCulture);
        // _lblPageTotal.Text = string.Format(MiscResources.OfN, _imageList.Images.Count);
        // if (!PlatformCompat.Runtime.IsToolbarTextboxSupported)
        // {
        //     _lblPageTotal.Text = _tbPageCurrent.Text + ' ' + _lblPageTotal.Text;
        // }
    }

    private async Task UpdateImage()
    {
        // TODO: Implement
        // _tiffViewer1.Image?.Dispose();
        // _tiffViewer1.Image = null;
        using var imageToRender = CurrentImage.GetClonedImage();
        var rendered = await Task.Run(() => imageToRender.Render());
        _renderedImage = rendered.ToEtoImage();
        ZoomToWindow();
        // _tiffViewer1.Image = imageToRender.RenderToBitmap();
    }

    private void Zoom(double step)
    {
        RenderSize = Size.Round(RenderSize * (float) Math.Pow(1.1, step));
        // TODO: Min/max?
    }

    private void ZoomToActual()
    {
        if (_renderedImage == null) return;
        RenderSize = new Size(_renderedImage.Width, _renderedImage.Height);
    }

    private void ZoomToWindow()
    {
        if (_renderedImage == null || !_scrollable.Loaded) return;
        if (_renderedImage.Width / (float) _renderedImage.Height > _scrollable.Width / (float) _scrollable.Height)
        {
            RenderSize = new Size(_scrollable.Width,
                (int) Math.Round(_renderedImage.Height * _scrollable.Width / (float) _renderedImage.Width));
        }
        else
        {
            RenderSize =
                new Size((int) Math.Round(_renderedImage.Width * _scrollable.Height / (float) _renderedImage.Height),
                    _scrollable.Height);
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ZoomToWindow();
    }

    // private async void tbPageCurrent_TextChanged(object sender, EventArgs e)
    // {
    //     if (int.TryParse(_tbPageCurrent.Text, out int indexOffBy1))
    //     {
    //         await GoTo(indexOffBy1 - 1);
    //     }
    // }

    private async Task DeleteCurrentImage()
    {
        var lastIndex = ImageIndex;
        if (MessageBox.Show(this,
                string.Format(MiscResources.ConfirmDeleteItems, 1),
                MiscResources.Delete, MessageBoxButtons.OKCancel,
                MessageBoxType.Question, MessageBoxDefaultButton.OK) == DialogResult.Ok)
        {
            // We don't want to run Commands.Delete as that runs on DesktopController and uses that selection.
            Commands.ImageListActions.DeleteSelected();
        }

        bool shouldClose = false;
        lock (ImageList)
        {
            if (ImageList.Images.Any())
            {
                // Update the GUI for the newly displayed image
                var nextIndex = lastIndex >= ImageList.Images.Count ? ImageList.Images.Count - 1 : lastIndex;
                CurrentImage = ImageList.Images[nextIndex];
                ImageList.UpdateSelection(ListSelection.Of(CurrentImage));
            }
            else
            {
                shouldClose = true;
                ImageList.UpdateSelection(ListSelection.Empty<UiImage>());
            }
        }
        if (shouldClose)
        {
            // No images left to display, so no point keeping the form open
            Close();
        }
        else
        {
            UpdatePage();
            await UpdateImage();
        }
    }

    protected override async void OnKeyDown(KeyEventArgs? e)
    {
        if (!(e.Control || e.Shift || e.Alt))
        {
            switch (e.Key)
            {
                case Keys.Escape:
                    Close();
                    return;
                // TODO: Left/right should maybe not change page if we're not at max zoom out (i.e. if we can pan)
                case Keys.PageDown:
                case Keys.Right:
                case Keys.Down:
                    await GoTo(ImageIndex + 1);
                    return;
                case Keys.PageUp:
                case Keys.Left:
                case Keys.Up:
                    await GoTo(ImageIndex - 1);
                    return;
            }
        }

        e.Handled = _ksm.Perform(e.KeyData);
    }

    private void AssignKeyboardShortcuts()
    {
        // Defaults

        _ksm.Assign("Del", DeleteCurrentImageCommand);
        _ksm.Assign("Ctrl+Oemplus", ZoomInCommand);
        _ksm.Assign("Ctrl+OemMinus", ZoomOutCommand);
        _ksm.Assign("Ctrl+0", ZoomActualCommand);

        // Configured

        var ks = Config.Get(c => c.KeyboardShortcuts);

        _ksm.Assign(ks.Delete, DeleteCurrentImageCommand);
        _ksm.Assign(ks.ImageBlackWhite, Commands.BlackWhite);
        _ksm.Assign(ks.ImageBrightness, Commands.BrightCont);
        _ksm.Assign(ks.ImageContrast, Commands.BrightCont);
        _ksm.Assign(ks.ImageCrop, Commands.Crop);
        _ksm.Assign(ks.ImageHue, Commands.HueSat);
        _ksm.Assign(ks.ImageSaturation, Commands.HueSat);
        _ksm.Assign(ks.ImageSharpen, Commands.Sharpen);

        _ksm.Assign(ks.RotateCustom, Commands.CustomRotate);
        _ksm.Assign(ks.RotateFlip, Commands.Flip);
        _ksm.Assign(ks.RotateLeft, Commands.RotateLeft);
        _ksm.Assign(ks.RotateRight, Commands.RotateRight);
        _ksm.Assign(ks.SaveImages, Commands.SaveSelectedImages);
        _ksm.Assign(ks.SavePDF, Commands.SaveSelectedPdf);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderedImage?.Dispose();
            _renderedImage = null;
        }
        base.Dispose(disposing);
    }
}
