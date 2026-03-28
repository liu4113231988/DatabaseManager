# AntdUI 迁移任务清单

> 本文件记录需要从原生 WinForms 控件迁移到 AntdUI 控件的表单和用户控件。

## 已完成

| 文件 | 控件数 | 完成时间 |
|------|--------|----------|
| UC_DbAccountInfo | 14 | 已完成 |
| frmDataFilterCondition | 16 | 已完成 |
| frmGenerateColumnDocumentation | 14 | 已完成 |
| frmGenerateScripts | 13 | 已完成 |
| frmExportData | 13 | 已完成 |
| frmCodeGenerator | 12 | 已完成 |
| frmTableCopy | 21 | 已完成 |
| UC_DataEditor | 10 | 已完成 |
| frmTranslateScript | 10 | 已完成 |
| frmDiagnose | 10 | 已完成 |
| frmDataTypeMappingSetting | 9 | 已完成 |
| frmImportData | 9 | 已完成 |
| frmDbConnect | 15 | 已完成 |
| UC_FileConnectionInfo | 8 | 已完成 |
| frmColumnMapping | 6 | 已完成 |
| frmColumnSelect | 3 | 已完成 |
| frmDataFilter | 7 | 已完成 |
| frmBackupSetting | 6 | 已完成 |
| frmDbConnectionManage | 8 | 已完成 |
| frmSetting | 2 | 已完成 |

---

## 待迁移 - 低优先级

| 文件 | 路径 | 控件类型统计 | 说明 |
|------|------|-------------|------|


---

## 需要特殊处理的控件

| 原控件 | AntdUI 对应 | 注意事项 |
|--------|-------------|----------|
| Button | AntdUI.Button | 低风险，直接替换 |
| TextBox | AntdUI.Input | 注意 Multiline、ReadOnly 属性 |
| ComboBox | AntdUI.Select | SelectedItem → SelectedValue，无 DropDownStyle |
| CheckBox | AntdUI.Checkbox | 无 UseVisualStyleBackColor |
| RadioButton | AntdUI.Radio | 无 UseVisualStyleBackColor、TabStop |
| Label | AntdUI.Label | 低风险，直接替换 |
| Panel | AntdUI.Panel | 无 TabStop |
| GroupBox | AntdUI.Panel | 需手动设置边框样式 |
| LinkLabel | AntdUI.Label | 使用不同事件处理方式 |
| DataGridView | AntdUI.Table | 高风险，需要重写列定义 |
| TreeView | AntdUI.Tree | 中风险，需要 TreeNode → TreeItem 映射 |
| TabControl | AntdUI.Tabs | 中风险，需要 TabPage → TabItem 映射 |
| PictureBox | 保持原样 | AntdUI 无直接对应 |
| ToolTip | 保持原样 | AntdUI 有 Tooltip 组件 |

---

## 迁移规则

1. **移除的属性**: `UseVisualStyleBackColor`、`DropDownStyle`、`FlatStyle`、`FormattingEnabled`、`TabStop`（部分控件）
2. **事件类型变更**:
   - `ComboBox.SelectedIndexChanged` → `AntdUI.IntEventHandler`
   - `RadioButton.CheckedChanged` → `AntdUI.BoolEventHandler`
3. **属性名称变更**:
   - `ComboBox.SelectedItem` → `AntdUI.Select.SelectedValue` 或 `SelectedValue`
   - `TextBox.ScrollBars` → 使用 `Multiline` 配合其他方式
   - `ComboBox.DisplayMember` → 使用 `Items` 集合

---

## 迁移步骤（参考）

1. 备份原始 Designer.cs 和 .cs 文件
2. 替换控件类型声明
3. 移除不兼容的属性设置
4. 修改事件处理签名
5. 更新代码中使用的属性名称
6. 编译测试
7. 功能测试

---

*最后更新: 2026-03-27*
