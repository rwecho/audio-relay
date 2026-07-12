using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System.Diagnostics.CodeAnalysis;
using QRCoder;

namespace AudioRelay.App;

[ExcludeFromCodeCoverage]
public partial class MainWindow : Window
{
    private readonly QRCodeGenerator _generator = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.QrPayload))
                    RenderQr(vm.QrPayload);
            };
            RenderQr(vm.QrPayload);
        }
    }

    private void RenderQr(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;

        var data = _generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        byte[] png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 16);

        var img = this.FindControl<Image>("QrImage");
        if (img is null) return;

        using var ms = new MemoryStream(png);
        img.Source = new Bitmap(ms);
    }
}
