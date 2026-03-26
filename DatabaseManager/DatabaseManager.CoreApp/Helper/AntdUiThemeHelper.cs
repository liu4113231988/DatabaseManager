using DatabaseManager.Core.Model;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace DatabaseManager.Helper
{
    internal static class AntdUiThemeHelper
    {
        public static readonly Color AppBackground = Color.FromArgb(245, 247, 250);
        public static readonly Color SurfaceBackground = Color.White;
        public static readonly Color SurfaceMuted = Color.FromArgb(248, 250, 252);
        public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);
        public static readonly Color TextColor = Color.FromArgb(15, 23, 42);
        public static readonly Color TextMutedColor = Color.FromArgb(100, 116, 139);
        public static readonly Color PrimaryColor = Color.FromArgb(22, 119, 255);

        private static readonly Font DefaultFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        private static readonly ToolStripRenderer ShellRenderer = new ToolStripProfessionalRenderer(new ModernToolStripColorTable());

        public static void ApplyGlobalTheme(ThemeType themeType)
        {
            AntdUI.Config.Theme()
                .Dark("#1F2937", "#F8FAFC")
                .Light("#FFFFFF", "#0F172A");

            AntdUI.Config.IsDark = themeType == ThemeType.Dark;

            AntdUI.Style.SetPrimary(PrimaryColor);
            AntdUI.Style.SetInfo(PrimaryColor);
            AntdUI.Style.SetSuccess(Color.FromArgb(34, 197, 94));
            AntdUI.Style.SetWarning(Color.FromArgb(245, 158, 11));
            AntdUI.Style.SetError(Color.FromArgb(239, 68, 68));
        }

        public static void ApplyMainShell(AntdUI.BaseForm form, MenuStrip menuStrip, ToolStrip toolStrip, DockPanel dockPanel)
        {
            ApplyFormSurface(form);

            form.Dark = AntdUI.Config.IsDark;
            form.Mode = AntdUI.Config.IsDark ? AntdUI.TAMode.Dark : AntdUI.TAMode.Light;
            form.AutoHandDpi = true;

            if (menuStrip != null)
            {
                menuStrip.BackColor = SurfaceBackground;
                menuStrip.ForeColor = TextColor;
                menuStrip.Font = DefaultFont;
                menuStrip.Padding = new Padding(8, 4, 0, 4);
                menuStrip.RenderMode = ToolStripRenderMode.Professional;
                menuStrip.Renderer = ShellRenderer;
            }

            if (toolStrip != null)
            {
                toolStrip.BackColor = SurfaceBackground;
                toolStrip.ForeColor = TextColor;
                toolStrip.Font = DefaultFont;
                toolStrip.Padding = new Padding(8, 6, 8, 6);
                toolStrip.RenderMode = ToolStripRenderMode.Professional;
                toolStrip.Renderer = ShellRenderer;
                toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            }

            if (dockPanel != null)
            {
                dockPanel.BackColor = AppBackground;
                dockPanel.ForeColor = TextColor;
                dockPanel.Font = DefaultFont;
            }
        }

        public static void ApplyDockWindow(Control control)
        {
            ApplyFormSurface(control);
            control.Padding = new Padding(12);
        }

        public static void ApplyFormSurface(Control control)
        {
            if (control == null)
            {
                return;
            }

            control.BackColor = AppBackground;
            control.ForeColor = TextColor;
            control.Font = DefaultFont;
        }

        public static void ApplyCard(AntdUI.Panel panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.Back = SurfaceBackground;
            panel.Radius = 12;
            panel.Shadow = 2;
            panel.ShadowColor = Color.FromArgb(24, 15, 23, 42);
            panel.ShadowOpacity = 0.10F;
            panel.InnerPadding = new Padding(12);
        }

        public static void ConfigureHeader(AntdUI.PageHeader header, string title, string subText, string description, Image icon = null)
        {
            if (header == null)
            {
                return;
            }

            header.Text = title;
            header.SubText = subText;
            header.Description = description;
            header.ShowIcon = icon != null;
            header.Icon = icon;
            header.UseTitleFont = true;
            header.UseTextBold = true;
            header.UseLeftMargin = true;
            header.ShowButton = false;
            header.DividerShow = true;
            header.BackColor = AppBackground;
        }

        public static void ConfigureActionButton(AntdUI.Button button, Image icon, bool primary = false)
        {
            if (button == null)
            {
                return;
            }

            button.Text = string.Empty;
            button.Icon = icon;
            button.AutoSize = false;
            button.Size = new Size(36, 32);
            button.Radius = 8;
            button.BorderWidth = 1;
            button.Ghost = !primary;
            button.Type = primary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        }

        public static void ConfigureMessageBox(AntdUI.Input input)
        {
            if (input == null)
            {
                return;
            }

            input.BorderWidth = 1;
            input.Radius = 10;
            input.Variant = AntdUI.TVariant.Outlined;
            input.Multiline = true;
            input.ReadOnly = true;
            input.AutoScroll = true;
            input.WordWrap = false;
            input.BackColor = SurfaceBackground;
        }

        public static ToolStripRenderer CreateRenderer() => ShellRenderer;

        private sealed class ModernToolStripColorTable : ProfessionalColorTable
        {
            public override Color ToolStripBorder => BorderColor;
            public override Color ToolStripDropDownBackground => SurfaceBackground;
            public override Color ToolStripGradientBegin => SurfaceBackground;
            public override Color ToolStripGradientMiddle => SurfaceBackground;
            public override Color ToolStripGradientEnd => SurfaceBackground;
            public override Color ImageMarginGradientBegin => SurfaceBackground;
            public override Color ImageMarginGradientMiddle => SurfaceBackground;
            public override Color ImageMarginGradientEnd => SurfaceBackground;
            public override Color MenuBorder => BorderColor;
            public override Color MenuItemBorder => Color.FromArgb(191, 219, 254);
            public override Color MenuItemSelected => Color.FromArgb(219, 234, 254);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(219, 234, 254);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(191, 219, 254);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(191, 219, 254);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(191, 219, 254);
            public override Color SeparatorLight => BorderColor;
            public override Color SeparatorDark => Color.FromArgb(209, 213, 219);
            public override Color CheckBackground => PrimaryColor;
            public override Color CheckPressedBackground => PrimaryColor;
            public override Color CheckSelectedBackground => Color.FromArgb(96, 165, 250);
        }
    }
}
