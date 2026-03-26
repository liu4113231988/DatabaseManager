# AntdUI 使用索引

> 本文件作为本仓库内 `AntdUI` 的优先检索文档。
>
> 以后查找控件用法时，先看这里；若这里没有，再查 AntdUI 官方文档或源码示例。

## 1. 查找顺序

1. 先查本文件 `antdui.md`
2. 再查 AntdUI 官方文档 `doc/wiki`
3. 再查官方示例和源码实现
4. 最后再联网查找补充信息

## 2. 选型结论

- 本项目已选定 `AntdUI` 作为后续界面现代化改造基础库。
- 适合本仓库这种数据库 IDE / 工具型桌面程序。
- 推荐先做“控件级替换”和“公共窗体统一”，不要一开始就强行重构整个主框架。

## 3. 官方参考入口

- 仓库主页：`https://github.com/AntdUI/AntdUI`
- 官方文档：`doc/wiki/en/Home.md`
- 安装说明：`doc/wiki/en/Install.md`
- 主题说明：`doc/wiki/en/Theme.md`
- 窗体基类：`doc/wiki/en/Form/BaseForm.md`

## 4. 快速安装

```powershell
Install-Package AntdUI
```

### 基础引用

```csharp
using AntdUI;
```

## 5. 主题与全局配置

AntdUI 的视觉核心是“主题 + 配色系统 + 暗色/亮色模式”。

### 5.1 设置主色

```csharp
AntdUI.Style.SetPrimary(Color.FromArgb(0, 173, 154));
```

### 5.2 设置语义色

```csharp
AntdUI.Style.SetSuccess(Color.FromArgb(82, 196, 26));
AntdUI.Style.SetWarning(Color.FromArgb(250, 173, 20));
AntdUI.Style.SetError(Color.FromArgb(245, 34, 45));
AntdUI.Style.SetInfo(Color.FromArgb(24, 144, 255));
```

### 5.3 切换深色模式

```csharp
AntdUI.Config.IsDark = true;
```

### 5.4 统一浅色/深色背景和文字

```csharp
AntdUI.Config.Theme()
    .Dark("#1e1e1e", "#ffffff")
    .Light("#ffffff", "#000000");
```

## 6. 窗体基类

AntdUI 提供了几种常用窗体基类，适合做统一风格窗口。

### 6.1 `BaseForm`

- 适合：普通主窗体、需要 DPI 支持的基础窗口
- 关键词：`AutoHandDpi`、`Dark`、`Mode`、`Theme()`

```csharp
public partial class FrmMain : AntdUI.BaseForm
{
    public FrmMain()
    {
        InitializeComponent();
    }
}
```

### 6.2 `Window`

- 适合：需要原生无边框效果的窗口
- 适合做：浮层窗口、独立工具窗、定制标题栏窗口

### 6.3 `BorderlessForm`

- 适合：带阴影的无边框窗口
- 适合做：登录窗、设置窗、弹窗、独立工具页

### 6.4 本项目建议

- `DatabaseManager.CoreApp` 的主工作区目前依赖 `DockPanelSuite`，建议先保留现有停靠结构。
- 优先用 AntdUI 替换：工具按钮、设置页、筛选页、选择器、提示框、表格、树、分页、输入框。
- 若后续需要整体无边框化，再考虑把顶层窗体切到 `BaseForm` / `Window`。

## 7. 主要控件速查

下面按“最常用、最适合本项目”的顺序整理。

### 7.1 Button

用途：执行操作、工具按钮、确认按钮、危险操作按钮。

关键词：`Text`、`Type`、`Ghost`、`BorderWidth`、`Radius`、`Icon`、`IconSvg`、`Loading`、`Toggle`

```csharp
var btnRun = new AntdUI.Button
{
    Text = "运行",
    Ghost = true,
    BorderWidth = 2,
    Radius = 8,
    Loading = false
};
```

常见用法：
- 工具栏按钮：`Ghost = true`，`Radius` 小一点
- 主操作按钮：强调主色，配合 `Style.SetPrimary(...)`
- 异步任务按钮：配合 `Loading = true`
- 可切换按钮：用 `Toggle = true`

### 7.2 FloatButton

用途：悬浮操作按钮，适合页面右下角快捷入口。

常见场景：
- 新建
- 快速刷新
- 回到顶部
- 运行当前动作

### 7.3 Input

用途：文本输入、查询条件、连接信息、脚本编辑辅助输入。

关键词：`Text`、`PlaceholderText`、`Variant`、`ReadOnly`、`Multiline`、`PrefixText`、`SuffixText`、`AllowClear`、`BorderWidth`、`Radius`

```csharp
var txtKeyword = new AntdUI.Input
{
    PlaceholderText = "请输入关键字",
    Radius = 8,
    AllowClear = true
};
```

常见用法：
- 单行输入：连接名、表名、列名、过滤条件
- 多行输入：SQL、JSON、脚本片段
- 只读输入：展示路径、结果摘要、ID

### 7.4 InputNumber

用途：数值输入，例如分页大小、超时时间、线程数、批量大小。

关键词：`Text`、`Value`、`Min`、`Max`、`Step`、`PlaceholderText`

### 7.5 Select

用途：单选下拉框，适合数据库类型、导出格式、主题类型等。

关键词：`Items`、`SelectedIndex`、`SelectedValue`、`DropDownArrow`、`List`、`ShowIcon`

```csharp
var cboDbType = new AntdUI.Select
{
    DropDownArrow = true,
    SelectedIndex = 0
};
```

常见用法：
- `SelectedIndexChanged` 作为主要响应事件
- 适合本项目中大量“数据库类型选择”“目标格式选择”的场景

### 7.6 SelectMultiple

用途：多选下拉框，适合批量选择字段、对象、过滤项。

关键词：`Items`、`SelectedValue`、`MaxChoiceCount`、`CheckMode`

### 7.7 DatePicker / DatePickerRange

用途：日期、时间范围筛选。

常见场景：
- 日志筛选
- 备份时间选择
- 数据查询时间范围

### 7.8 Menu

用途：导航菜单、侧边菜单、顶部菜单、上下文菜单式导航。

关键词：`Items`、`Mode`、`Collapsed`、`Indent`、`Unique`、`Trigger`、`SelectChanged`

```csharp
// 典型用途：左侧导航树或顶部功能导航
```

常见用法：
- `Mode = Inline`：侧边栏
- `Mode = Horizontal`：顶部导航
- `Collapsed = true`：收起侧栏
- `Unique = true`：仅展开一个分组

### 7.9 PageHeader

用途：页面标题区、面包屑式标题、操作区说明。

适合放在：
- 设置页
- 列表页
- 详情页

### 7.10 Breadcrumb

用途：层级导航提示。

适合本项目的场景：
- 数据库 -> Schema -> Table -> Column
- 功能页路径提示

### 7.11 Tabs / TabHeader

用途：多页切换。

常见场景：
- 多个查询页
- 多个设置分类
- 多个对比结果页

### 7.12 Pagination

用途：分页控件。

适合本项目的场景：
- 数据查看
- 对比结果
- 导入导出预览

### 7.13 Steps

用途：多步骤向导。

适合本项目的场景：
- 数据库转换
- 表复制
- 导入向导

### 7.14 Panel

用途：承载内容区、分块展示。

### 7.15 Divider

用途：分割内容区域。

### 7.16 StackPanel / FlowPanel / GridPanel / Splitter

用途：布局控件。

- `StackPanel`：横向/纵向排列
- `FlowPanel`：自动换行布局
- `GridPanel`：网格布局
- `Splitter`：拖拽分割区域

### 7.17 Table

用途：数据表格、比较结果、列表展示，是本项目最关键的控件之一。

关键词：`Columns`、`DataSource`、`FixedHeader`、`Bordered`、`RowHeight`、`VirtualMode`、`EditMode`、`RowSelectedBg`、`EmptyText`

```csharp
// 常见思路：绑定数据源后，配合列定义展示结果
```

建议重点关注：
- 数据查看页
- 导入预览页
- Schema / Data 对比结果页
- 统计结果页

### 7.18 Tree

用途：树形结构。

适合本项目的场景：
- 数据库对象树
- 表关系树
- 依赖树
- 分类导航树

### 7.19 Tag / Badge / Label

用途：状态标记、计数标记、轻量文本提示。

常见场景：
- 连接状态
- 数据库类型标识
- 差异数量统计
- 错误/警告状态

### 7.20 Tooltip / Popover

用途：悬停提示、局部信息解释。

### 7.21 ImagePreview / SVG

用途：图片预览与矢量图标。

适合本项目的场景：
- 图像字段查看
- 图标统一化
- 结果预览

## 8. 反馈类控件

### 8.1 Message

用途：全局消息提示。

适合：
- 成功提示
- 普通提示
- 操作完成反馈

### 8.2 Modal

用途：模态确认框。

适合：
- 删除确认
- 覆盖确认
- 风险操作确认

### 8.3 Notification

用途：右上角非阻塞通知。

适合：
- 后台任务开始/结束
- 长耗时任务提示

### 8.4 Alert

用途：页面内警告块。

### 8.5 Drawer

用途：侧滑面板。

适合：
- 详情侧栏
- 高级筛选
- 配置面板

### 8.6 Progress / Spin

用途：进度条、加载中状态。

适合：
- 数据获取
- 导出
- 转换
- 比较

## 9. 本项目推荐优先替换的界面

### 第一批：收益最高

- 设置窗口
- 连接管理窗口
- 选择器窗口
- 确认框 / 提示框
- 导入导出向导
- 转换与对比结果窗口

### 第二批：核心业务界面

- SQL 查询页
- 数据查看页
- 对象浏览器
- 统计页面
- 诊断页面

### 第三批：增强体验

- 空状态提示
- 加载中状态
- 右键菜单
- 标题区与分页区

## 10. 常用属性速查

### Button

- `Text`
- `Type`
- `Ghost`
- `BorderWidth`
- `Radius`
- `Icon`
- `IconSvg`
- `Loading`
- `Toggle`

### Input

- `Text`
- `PlaceholderText`
- `ReadOnly`
- `Multiline`
- `Variant`
- `AllowClear`
- `PrefixText`
- `SuffixText`

### Select

- `Items`
- `SelectedIndex`
- `SelectedValue`
- `DropDownArrow`
- `List`

### Table

- `Columns`
- `DataSource`
- `FixedHeader`
- `VirtualMode`
- `EditMode`
- `EmptyText`
- `RowSelectedBg`

### Menu

- `Items`
- `Mode`
- `Collapsed`
- `Unique`
- `Indent`

## 11. 更新规则

- 新增控件前，先检查本文件是否已有对应条目。
- 若已有条目，优先补充“在本项目中的用法”和“常用属性”。
- 若没有条目，先补一条索引，再补示例。
- 如果本文件没有答案，再去官方文档查。

## 12. 待补充清单

- `ContextMenuStrip`
- `Drawer`
- `Popover`
- `Tabs`
- `Tree`
- `Table` 高级绑定示例
- `ThemeConfig` 更完整的项目内推荐配置
- `AntdUI` 与 `DatabaseManager.CoreApp` 现有 Dock 界面的整合示例

## 13. DatabaseManager.CoreApp 迁移对照表

> 下面是本项目最常见的 WinForms 控件到 AntdUI 的建议映射。
>
> 迁移时优先保证“外观统一 + 交互不变”，不要一次性把所有控件全部重写。

| 现有控件 | 推荐 AntdUI 控件 | 典型场景 | 迁移建议 |
| :-- | :-- | :-- | :-- |
| `Button` | `AntdUI.Button` | 确认、提交、刷新、运行 | 低风险，优先替换 |
| `ToolStripButton` | `AntdUI.Button` / `FloatButton` | 顶部工具栏操作 | 先统一样式，后续再重构布局 |
| `TextBox` | `AntdUI.Input` | 连接信息、过滤条件、脚本输入 | 低风险，优先替换 |
| `RichTextBox` | `AntdUI.Input`（`Multiline=true`） | 日志、消息、脚本内容 | 适合只读/轻编辑场景 |
| `ComboBox` | `AntdUI.Select` | 数据库类型、账号、导出格式 | 中风险，涉及数据绑定方式变化 |
| `CheckedListBox` | `AntdUI.SelectMultiple` | 批量选择字段、对象、选项 | 适合多选配置页 |
| `Label` | `AntdUI.Label` | 标题、说明、状态提示 | 低风险，统一字号与颜色 |
| `CheckBox` | `AntdUI.Checkbox` | 选项开关 | 低风险 |
| `RadioButton` | `AntdUI.Radio` | 模式切换、二选一 | 低风险 |
| `Panel` / `GroupBox` | `AntdUI.Panel` + `Divider` / `PageHeader` | 分区、卡片、内容块 | 先统一容器背景和留白 |
| `SplitContainer` | `AntdUI.Splitter` + `Panel` / `GridPanel` | 左右分栏、上下分栏 | 中风险，适合后续重构布局 |
| `MenuStrip` | `AntdUI.Menu` / `PageHeader` | 顶部导航、功能入口 | 建议分阶段迁移 |
| `ToolStrip` | `AntdUI.Button` 组 / `PageHeader` 动作区 | 工具栏 | 先做渲染统一，再决定是否重做 |
| `TreeView` | `AntdUI.Tree` | 对象浏览器、依赖树、关系树 | 中高风险，需要 `TreeNode` -> `TreeItem` 映射 |
| `DataGridView` | `AntdUI.Table` | 数据查看、比较结果、导入预览 | 高价值，建议分页面迁移 |
| `ObjectListView` | `AntdUI.Table` | 列表/表格展示 | 高价值，优先规划 |
| `TabControl` | `AntdUI.Tabs` / `TabHeader` | 多页切换、设置页、查询页 | 中风险 |
| `StatusStrip` | `AntdUI.PageHeader` / `Label` / `Panel` | 状态、统计、提示 | 适合改成轻量状态区 |
| `NumericUpDown` | `AntdUI.InputNumber` | 数字参数、批量大小、超时 | 低风险 |
| `DateTimePicker` | `AntdUI.DatePicker` / `DatePickerRange` | 时间筛选、备份时间 | 低风险 |
| `PictureBox` | `AntdUI.ImagePreview` / `AntdUI.Avatar` | 图片预览、图标展示 | 低风险 |
| `ContextMenuStrip` | `AntdUI.ContextMenuStrip` | 右键菜单、操作菜单 | 低风险，适合公共菜单统一 |
| `FlowLayoutPanel` | `AntdUI.FlowPanel` | 标签组、按钮组、筛选区 | 低风险 |
| `TableLayoutPanel` | `AntdUI.GridPanel` | 表单布局、详情布局 | 中风险 |

### 13.1 本项目优先迁移顺序

1. `Button`、`TextBox`、`ComboBox`、`Label`
2. 公共容器：`frmMain`、`frmDockWindowBase`、`frmMessage`、`frmContent`、`frmObjectsExplorer`
3. `TreeView`、`DataGridView`、`ObjectListView`
4. 设置页、对话框、筛选页、导入导出页
5. 复杂页面的布局重构

### 13.2 当前已确认的本项目可优先替换点

- `frmMessage`：适合先替换成 `AntdUI.Input` 作为消息面板。
- `UC_DbObjectsExplorer` 顶部按钮：适合先换成 `AntdUI.Button`。
- `frmMain` 顶部菜单/工具栏：适合先统一渲染风格，再决定是否拆成 AntdUI 动作区。
- `frmDockWindowBase` 的右键菜单：适合先统一样式。
