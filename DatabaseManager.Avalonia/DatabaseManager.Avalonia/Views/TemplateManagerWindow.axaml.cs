using Avalonia.Controls;
using DatabaseManager.AppCore.ViewModels;

namespace DatabaseManager.Avalonia.Views;

public partial class TemplateManagerWindow : Window
{
    public TemplateManagerWindow() => InitializeComponent();
    public TemplateManagerWindow(TemplateManagerViewModel vm) : this() => DataContext = vm;
}
