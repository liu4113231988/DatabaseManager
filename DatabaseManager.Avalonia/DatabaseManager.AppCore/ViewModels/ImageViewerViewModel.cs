using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseManager.AppCore.Common;
using DatabaseInterpreter.Utility;
using System.IO;

namespace DatabaseManager.AppCore.ViewModels;

/// <summary>
/// 图像查看器 ViewModel（工具菜单）。
/// 对应原 WinForms frmImageViewer：输入十六进制内容，识别格式并预览图片。
/// </summary>
public partial class ImageViewerViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _hexText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _detectedExtension = string.Empty;

    /// <summary>解码后的图片字节（供视图层生成 Bitmap）。</summary>
    [ObservableProperty]
    private byte[]? _imageBytes;

    private static readonly (byte[] magic, string extension)[] ImageFormats =
    [
        (new byte[] { 0xFF, 0xD8 }, "jpg"),
        (new byte[] { 0x42, 0x4D }, "bmp"),
        (new byte[] { 0x47, 0x49, 0x46 }, "gif"),
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png"),
        (new byte[] { 0x49, 0x49, 0x2A, 0x00 }, "tif"),
        (new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, "tif"),
    ];

    [RelayCommand]
    private void ViewImage()
    {
        if (string.IsNullOrWhiteSpace(HexText))
        {
            StatusMessage = "请输入十六进制内容。";
            return;
        }

        try
        {
            var content = HexText.Trim();
            var bytes = ValueHelper.HexStringToBytes(content);

            // OLE 头处理（原逻辑：0x15 开头跳过 78 字节）
            if (content.StartsWith("0x15", StringComparison.OrdinalIgnoreCase) && bytes.Length > 78)
            {
                bytes = bytes.Skip(78).ToArray();
            }

            var ext = TryGetExtension(bytes);
            DetectedExtension = ext ?? "未知";
            ImageBytes = bytes;
            StatusMessage = ext is null ? "已加载（未识别格式）" : $"已识别格式：{ext}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"预览失败：{ex.Message}";
            ImageBytes = null;
            DetectedExtension = string.Empty;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        HexText = string.Empty;
        ImageBytes = null;
        DetectedExtension = string.Empty;
        StatusMessage = string.Empty;
    }

    private static string? TryGetExtension(byte[] array)
    {
        foreach (var (magic, extension) in ImageFormats)
        {
            if (IsMatch(array, magic))
            {
                // SVG 需要特殊处理：检查 XML 声明后是否含 <svg
                if (extension == "svg") continue;
                return extension;
            }
        }

        // SVG 检测
        var svgSmall = new byte[] { 0x3C, 0x73, 0x76, 0x67 };
        var svgCapital = new byte[] { 0x3C, 0x53, 0x56, 0x47 };
        if (IsMatch(array, svgSmall) || IsMatch(array, svgCapital))
            return "svg";

        // 带 XML 声明的 SVG
        var xmlSmall = new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C };
        var xmlCapital = new byte[] { 0x3C, 0x3F, 0x58, 0x4D, 0x4C };
        if (IsMatch(array, xmlSmall) || IsMatch(array, xmlCapital))
        {
            int max = Math.Min(1024, array.Length);
            for (int i = 5; i < max; i++)
            {
                if (IsMatch(array, svgSmall, i) || IsMatch(array, svgCapital, i))
                    return "svg";
            }
        }

        return null;
    }

    private static bool IsMatch(byte[] array, byte[] magic, int offset = 0)
    {
        if (offset + magic.Length > array.Length) return false;
        for (int i = 0; i < magic.Length; i++)
            if (array[offset + i] != magic[i]) return false;
        return true;
    }
}
