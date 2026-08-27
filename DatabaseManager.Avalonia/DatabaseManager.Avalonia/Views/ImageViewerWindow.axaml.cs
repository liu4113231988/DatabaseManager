using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using DatabaseManager.AppCore.ViewModels;
using System.ComponentModel;
using System.IO;

namespace DatabaseManager.Avalonia.Views;

/// <summary>
/// 图像查看器窗口。对应原 WinForms frmImageViewer。
/// </summary>
public partial class ImageViewerWindow : Window
{
    private readonly ImageViewerViewModel? _vm;

    public ImageViewerWindow()
    {
        InitializeComponent();
    }

    public ImageViewerWindow(ImageViewerViewModel vm) : this()
    {
        DataContext = vm;
        _vm = vm;
        if (_vm is not null)
            _vm.PropertyChanged += Vm_PropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Vm_PropertyChanged;
        base.OnClosed(e);
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewerViewModel.ImageBytes))
            UpdateImage();
    }

    private void UpdateImage()
    {
        var imageControl = this.FindControl<Image>("PreviewImage");
        if (imageControl is null || _vm?.ImageBytes is null || _vm.ImageBytes.Length == 0)
        {
            if (imageControl is not null) imageControl.Source = null;
            return;
        }

        try
        {
            using var ms = new MemoryStream(_vm.ImageBytes);
            var bitmap = new Bitmap(ms);
            imageControl.Source = bitmap;
        }
        catch
        {
            imageControl.Source = null;
        }
    }

    private void BtnClose_Click(object? sender, RoutedEventArgs e) => Close();
}
