﻿﻿using DatabaseConverter.Model;
using DatabaseInterpreter.Core;
using DatabaseInterpreter.Model;
using DatabaseInterpreter.Utility;
using DatabaseManager.Core;
using DatabaseManager.Core.Model;
using DatabaseManager.Forms;
using DatabaseManager.Helper;
using DatabaseManager.Model;
using DatabaseManager.Profile.Manager;
using DatabaseManager.Profile.Model;
using AntdUI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Function = DatabaseInterpreter.Model.Function;
using View = DatabaseInterpreter.Model.View;
using Table = DatabaseInterpreter.Model.Table;

namespace DatabaseManager.Controls
{
    public delegate void ShowDbObjectContentHandler(DatabaseObjectDisplayInfo content);

    public partial class UC_DbObjectsComplexTree : UserControl, IObserver<FeedbackInfo>
    {
        private DatabaseType databaseType;
        private ConnectionInfo connectionInfo;
        private DbInterpreterOption simpleInterpreterOption = new DbInterpreterOption() { ObjectFetchMode = DatabaseObjectFetchMode.Simple, ThrowExceptionWhenErrorOccurs = true };

        public ShowDbObjectContentHandler OnShowContent;
        public DatabaseInterpreter.Utility.FeedbackHandler OnFeedback;

        public DatabaseType DatabaseType => this.databaseType;

        public UC_DbObjectsComplexTree()
        {
            InitializeComponent();

            FormEventCenter.OnRefreshNavigatorFolder += this.RefreshFolderNode;

            TreeView.CheckForIllegalCrossThreadCalls = false;
            Form.CheckForIllegalCrossThreadCalls = false;

            tvDbObjects.BeforeExpand += (sender, e) =>
            {
                if (!this.IsOnlyHasFakeChild(e.Item))
                {
                    e.CanExpand = true;
                    return;
                }

                Task.Run(async () =>
                {
                    await this.LoadChildItems(e.Item);
                });
            };

            tvDbObjects.AfterExpand += (sender, e) =>
            {
                this.Feedback("");
            };

            tvDbObjects.NodeMouseDoubleClick += tvDbObjects_NodeMouseDoubleClick;
            tvDbObjects.KeyDown += TvDbObjects_KeyDown;
        }

        private void TvDbObjects_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    this.RefreshFolderNode();
                    break;
                case Keys.Delete:
                    if (this.CanDelete(this.tvDbObjects.SelectItem))
                    {
                        this.tsmiDelete_Click(sender, e);
                    }
                    break;
                case Keys.F2:
                    break;
                case Keys.Enter:
                    this.tvDbObjects_NodeMouseDoubleClick(sender, e);
                    break;
                case Keys.Control | Keys.C:
                    var item = this.tvDbObjects.SelectItem;
                    if (item != null)
                    {
                        Clipboard.SetText(item.Text);
                        this.Feedback($"Copied: {item.Text}");
                    }
                    break;
            }
        }

        public async Task LoadTree(DatabaseType dbType, ConnectionInfo connectionInfo)
        {
            this.databaseType = dbType;
            this.connectionInfo = connectionInfo;

            this.tvDbObjects.Items.Clear();

            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, connectionInfo, simpleInterpreterOption);

            List<Database> databases = await dbInterpreter.GetDatabasesAsync();

            IEnumerable<DatabaseVisibilityInfo> visibilities = Enumerable.Empty<DatabaseVisibilityInfo>();

            bool isFileConnection = ManagerUtil.IsFileConnection(dbType);

            if (!isFileConnection && dbType != DatabaseType.Oracle)
            {
                AccountProfileInfo profileInfo = await AccountProfileManager.GetProfile(dbType.ToString(), connectionInfo.Server, connectionInfo.Port, connectionInfo.IntegratedSecurity, connectionInfo.UserId);

                if (profileInfo != null)
                {
                    visibilities = await DatabaseVisibilityManager.GetVisibilities(profileInfo.Id);
                }
            }

            foreach (Database database in databases)
            {
                if (visibilities.Any(item => item.Hidden && item.Database.ToUpper() == database.Name.ToUpper()))
                {
                    continue;
                }

                var item = DbObjectsAntdTreeHelper.CreateTreeItem(database, true);

                if (ManagerUtil.IsFileConnection(dbType))
                {
                    FileConnectionProfileInfo profile = await FileConnectionProfileManager.GetProfileByDatabase(dbType.ToString(), connectionInfo.Database);

                    if (profile != null)
                    {
                        item.Text = profile.Name;
                    }
                }

                this.tvDbObjects.Items.Add(item);
            }

            if (this.tvDbObjects.Items.Count == 1)
            {
                this.tvDbObjects.SelectItem = this.tvDbObjects.Items[0];
                this.tvDbObjects.Items[0].Expand = true;
            }
        }

        public void ClearNodes()
        {
            this.tvDbObjects.Items.Clear();
        }

        private void TvDbObjects_NodeMouseClick(TreeItem item, Rectangle rect, TreeCType type, MouseEventArgs args)
        {
            if (args.Button == MouseButtons.Right)
            {
                if (item.Name == DbObjectsAntdTreeHelper.FakeNodeName)
                {
                    return;
                }

                this.tvDbObjects.SelectItem = item;

                this.SetMenuItemVisible(item);

                this.contextMenuStrip1.Show(Cursor.Position);
            }
        }

        private bool CanRefresh(AntdUI.TreeItem item)
        {
            return (GetItemLevel(item) <= 3) && !this.IsOnlyHasFakeChild(item)
               && !(item.Tag is ScriptDbObject && !(item.Tag is View))
               && !(item.Tag is UserDefinedType)
               && !(item.Tag is Sequence);
        }

        private bool CanDelete(AntdUI.TreeItem item)
        {
            int level = GetItemLevel(item);
            return level == 2 || (level == 4 && !(item.Tag is TableColumn));
        }

        private int GetItemLevel(AntdUI.TreeItem item)
        {
            int level = 0;
            var parent = GetParentItem(item);
            while (parent != null)
            {
                level++;
                parent = GetParentItem(parent);
            }
            return level;
        }

        private AntdUI.TreeItem GetParentItem(AntdUI.TreeItem item)
        {
            foreach (var root in tvDbObjects.Items)
            {
                var found = FindParentItem(root, item);
                if (found != null)
                    return found;
            }
            return null;
        }

        private AntdUI.TreeItem FindParentItem(AntdUI.TreeItem parent, AntdUI.TreeItem target)
        {
            foreach (var child in parent.Sub)
            {
                if (child == target)
                    return parent;
                var found = FindParentItem(child, target);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var item = this.tvDbObjects.SelectItem;
            if (item != null)
            {
                this.SetMenuItemVisible(item);
            }
        }

        private void SetMenuItemVisible(AntdUI.TreeItem item)
        {
            int level = GetItemLevel(item);
            bool isDatabase = level == 0;
            bool isView = item.Tag is View;
            bool isTable = item.Tag is Table;
            bool isScriptObject = item.Tag is ScriptDbObject;
            bool isUserDefinedType = item.Tag is UserDefinedType;
            bool isSequence = item.Tag is Sequence;
            bool isFunction = item.Tag is Function;
            bool isTriggerFunction = isFunction && (item.Tag as Function).IsTriggerFunction;
            bool isProcedure = item.Tag is Procedure;
            bool isColumn = item.Tag is TableColumn;
            bool isColumnsFolder = item.Name == nameof(DbObjectTreeFolderType.Columns);

            this.tsmiNewQuery.Visible = isDatabase;
            this.tsmiNewTable.Visible = item.Name == nameof(DbObjectTreeFolderType.Tables) || isTable;
            this.tsmiNewView.Visible = item.Name == nameof(DbObjectTreeFolderType.Views) || isView;
            this.tsmiNewFunction.Visible = item.Name == nameof(DbObjectTreeFolderType.Functions) || item.Tag is Function;
            this.tsmiNewProcedure.Visible = item.Name == nameof(DbObjectTreeFolderType.Procedures) || item.Tag is Procedure;
            this.tsmiNewTrigger.Visible = item.Name == nameof(DbObjectTreeFolderType.Triggers) || item.Tag is TableTrigger;
            this.tsmiAlter.Visible = isScriptObject;
            this.tsmiDesign.Visible = isTable;
            this.tsmiCopy.Visible = isTable;
            this.tsmiRefresh.Visible = this.CanRefresh(item);
            this.tsmiGenerateScripts.Visible = isDatabase || isTable || isScriptObject || isUserDefinedType || isSequence;
            this.tsmiConvert.Visible = isDatabase;
            this.tsmiEmptyDatabase.Visible = isDatabase;
            this.tsmiDelete.Visible = this.CanDelete(item);
            this.tsmiViewData.Visible = isTable || isView;
            this.tsmiImportData.Visible = isTable;
            this.tsmiExportData.Visible = isTable || isView;
            this.tsmiEditData.Visible = isTable;
            this.tsmiTranslate.Visible = isTable || isUserDefinedType || isSequence || isScriptObject;
            this.tsmiMore.Visible = isDatabase;
            this.tsmiBackup.Visible = isDatabase;
            this.tsmiDiagnose.Visible = isDatabase;
            this.tsmiCompareSchema.Visible = isDatabase;
            this.tsmiCompareData.Visible = isDatabase;
            this.tsmiClearData.Visible = isDatabase;
            this.tsmiOptimize.Visible = (this.databaseType == DatabaseType.MySql || this.databaseType == DatabaseType.Sqlite);

            this.tsmiSelectScript.Visible = isTable || isView || (isFunction && !isTriggerFunction);
            this.tsmiInsertScript.Visible = isTable;
            this.tsmiUpdateScript.Visible = isTable;
            this.tsmiDeleteScript.Visible = isTable;
            this.tsmiGenerateProcedure.Visible = isTable && this.databaseType != DatabaseType.Sqlite;
            this.tsmiViewDependency.Visible = isDatabase || ((isTable || isView || isFunction || isProcedure) && this.databaseType != DatabaseType.Sqlite);
            this.tsmiExecuteScript.Visible = isProcedure;
            this.tsmiDatabaseDiagram.Visible = isDatabase;
            this.tsmiAnalysis.Visible = (this.databaseType == DatabaseType.SqlServer || this.databaseType == DatabaseType.Postgres || this.databaseType == DatabaseType.Oracle);
            this.tsmiDisconnect.Visible = isDatabase;

            this.tsmiCopyChildrenNames.Visible = level == 1 && item.Sub.Count > 0 && (item.Sub[0].Tag != null);

            this.tsmiNewColumn.Visible = isColumnsFolder;
            this.tsmiModifyColumn.Visible = isColumn;
        }

        private ConnectionInfo GetConnectionInfo(string database)
        {
            ConnectionInfo info = ObjectHelper.CloneObject<ConnectionInfo>(this.connectionInfo);
            info.Database = database;
            return info;
        }

        public ConnectionInfo GetCurrentConnectionInfo()
        {
            var item = this.tvDbObjects.SelectItem;

            if (item != null)
            {
                var dbItem = this.GetDatabaseItem(item);
                ConnectionInfo connectionInfo = this.GetConnectionInfo(dbItem.Name);

                return connectionInfo;
            }

            return null;
        }

        private bool IsOnlyHasFakeChild(AntdUI.TreeItem item)
        {
            if (item.Sub.Count == 1 && item.Sub[0].Name == DbObjectsAntdTreeHelper.FakeNodeName)
            {
                return true;
            }

            return false;
        }

        private AntdUI.TreeItem GetDatabaseItem(AntdUI.TreeItem item)
        {
            while (!(item.Tag is Database))
            {
                var parent = GetParentItem(item);
                if (parent == null) return item;
                item = parent;
            }
            return item;
        }

        private DbInterpreter GetDbInterpreter(string database, bool isSimpleMode = true, bool throwExceptionWhenErrorOccurs = false)
        {
            ConnectionInfo connectionInfo = this.GetConnectionInfo(database);

            DbInterpreterOption option = isSimpleMode ? simpleInterpreterOption : new DbInterpreterOption() { ObjectFetchMode = DatabaseObjectFetchMode.Details };

            option.ThrowExceptionWhenErrorOccurs = throwExceptionWhenErrorOccurs;

            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(this.databaseType, connectionInfo, option);

            return dbInterpreter;
        }

        private async Task AddDbObjectItems(AntdUI.TreeItem parentItem, string database, DatabaseObjectType databaseObjectType = DatabaseObjectType.None, bool createFolderItem = true)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);

            SchemaInfoFilter filter = new SchemaInfoFilter() { DatabaseObjectType = databaseObjectType };

            if (this.databaseType == DatabaseType.Oracle)
            {
                filter.Schema = database;
            }

            SchemaInfo schemaInfo = databaseObjectType == DatabaseObjectType.None ? new SchemaInfo() :
                                    await dbInterpreter.GetSchemaInfoAsync(filter);

            this.ClearItems(parentItem);

            if (databaseObjectType == DatabaseObjectType.Table)
            {
                this.AddTreeItems(parentItem, databaseObjectType, DatabaseObjectType.Table, schemaInfo.Tables, createFolderItem, true);
            }

            if (databaseObjectType == DatabaseObjectType.View)
            {
                this.AddTreeItems(parentItem, databaseObjectType, DatabaseObjectType.View, schemaInfo.Views, createFolderItem, true);
            }

            if (databaseObjectType == DatabaseObjectType.Function)
            {
                this.AddTreeItems(parentItem, databaseObjectType, DatabaseObjectType.Function, schemaInfo.Functions, createFolderItem);
            }

            if (databaseObjectType == DatabaseObjectType.Procedure)
            {
                this.AddTreeItems(parentItem, databaseObjectType, DatabaseObjectType.Procedure, schemaInfo.Procedures, createFolderItem);
            }

            if (databaseObjectType == DatabaseObjectType.Type)
            {
                foreach (UserDefinedType userDefinedType in schemaInfo.UserDefinedTypes)
                {
                    string dataType = userDefinedType.Attributes.Count > 1 ? "" : userDefinedType.Attributes.First().DataType;
                    string strDataType = string.IsNullOrEmpty(dataType) ? "" : $"({dataType})";

                    string text = $"{userDefinedType.Name}{strDataType}";

                    var treeItem = DbObjectsAntdTreeHelper.CreateTreeItem(userDefinedType.Name, text, nameof(userDefinedType));
                    treeItem.Tag = userDefinedType;

                    parentItem.Sub.Add(treeItem);
                }
            }

            if (databaseObjectType == DatabaseObjectType.Sequence)
            {
                this.AddTreeItems(parentItem, databaseObjectType, DatabaseObjectType.Sequence, schemaInfo.Sequences, createFolderItem);
            }
        }

        private void AddTreeItems<T>(AntdUI.TreeItem item, DatabaseObjectType types, DatabaseObjectType type, List<T> dbObjects, bool createFolderItem = true, bool createFakeItem = false)
            where T : DatabaseObject
        {
            AntdUI.TreeItem targetItem = item;

            if (types.HasFlag(type))
            {
                bool showSchemaName = this.NeedShowSchema(item, dbObjects);

                if (createFolderItem)
                {
                    targetItem = item.AddDbObjectFolderItem(dbObjects);
                }
                else
                {
                    item.AddDbObjectItems(dbObjects, showSchemaName);
                }
            }

            if (createFakeItem && targetItem != null)
            {
                foreach (var child in targetItem.Sub)
                {
                    child.Sub.Add(DbObjectsAntdTreeHelper.CreateFakeItem());
                }
            }
        }

        private bool NeedShowSchema<T>(AntdUI.TreeItem item, IEnumerable<T> dbObjects) where T : DatabaseObject
        {
            var dbInterpreter = this.GetDbInterpreter(this.GetDatabaseItem(item).Name, true);

            return DbObjectsAntdTreeHelper.NeedShowSchema(dbInterpreter, dbObjects);
        }

        private void AddTableFakeItems(AntdUI.TreeItem tableItem, Table table)
        {
            this.ClearItems(tableItem);

            var columnsItem = DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Columns), nameof(DbObjectTreeFolderType.Columns), true);
            var triggersItem = DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Triggers), nameof(DbObjectTreeFolderType.Triggers), true);
            var indexesItem = DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Indexes), nameof(DbObjectTreeFolderType.Indexes), true);
            var keysItem = DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Keys), nameof(DbObjectTreeFolderType.Keys), true);
            var constraintsItem = DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Constraints), nameof(DbObjectTreeFolderType.Constraints), true);

            tableItem.Sub.Add(columnsItem);
            tableItem.Sub.Add(triggersItem);
            tableItem.Sub.Add(indexesItem);
            tableItem.Sub.Add(keysItem);
            tableItem.Sub.Add(constraintsItem);
        }

        private void AddViewFakeItems(AntdUI.TreeItem viewItem, View view)
        {
            this.ClearItems(viewItem);

            viewItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Columns), nameof(DbObjectTreeFolderType.Columns), true));

            if (this.databaseType == DatabaseType.SqlServer || this.databaseType == DatabaseType.Sqlite)
            {
                viewItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Triggers), nameof(DbObjectTreeFolderType.Triggers), true));
            }

            if (this.databaseType == DatabaseType.SqlServer)
            {
                viewItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Indexes), nameof(DbObjectTreeFolderType.Indexes), true));
            }
        }

        private void AddDatabaseFakeItems(AntdUI.TreeItem databaseItem, Database database)
        {
            this.ClearItems(databaseItem);

            DbInterpreter dbInterpreter = this.GetDbInterpreter(database.Name, true);

            DatabaseObjectType supportDbObjectType = dbInterpreter.SupportDbObjectType;

            databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Tables), nameof(DbObjectTreeFolderType.Tables), true));
            databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Views), nameof(DbObjectTreeFolderType.Views), true));

            if (supportDbObjectType.HasFlag(DatabaseObjectType.Function))
            {
                databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Functions), nameof(DbObjectTreeFolderType.Functions), true));
            }

            if (supportDbObjectType.HasFlag(DatabaseObjectType.Procedure))
            {
                databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Procedures), nameof(DbObjectTreeFolderType.Procedures), true));
            }

            if (supportDbObjectType.HasFlag(DatabaseObjectType.Type))
            {
                databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Types), nameof(DbObjectTreeFolderType.Types), true));
            }

            if (supportDbObjectType.HasFlag(DatabaseObjectType.Sequence))
            {
                databaseItem.Sub.Add(DbObjectsAntdTreeHelper.CreateFolderItem(nameof(DbObjectTreeFolderType.Sequences), nameof(DbObjectTreeFolderType.Sequences), true));
            }
        }

        private async Task AddTableObjectItems(AntdUI.TreeItem treeItem, Table table, DatabaseObjectType databaseObjectType, bool isForView = false)
        {
            string itemName = treeItem.Name;
            string database = this.GetDatabaseItem(treeItem).Name;
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database, false);

            dbInterpreter.Subscribe(this);

            SchemaInfoFilter filter = new SchemaInfoFilter() { Strict = true, DatabaseObjectType = databaseObjectType, Schema = table.Schema, TableNames = new string[] { table.Name } };

            if (isForView)
            {
                filter.ColumnType = ColumnType.ViewColumn;
                filter.IsForView = true;
            }

            SchemaInfo schemaInfo = await dbInterpreter.GetSchemaInfoAsync(filter);

            this.ClearItems(treeItem);

            #region Columns           
            if (itemName == nameof(DbObjectTreeFolderType.Columns))
            {
                foreach (TableColumn column in schemaInfo.TableColumns)
                {
                    string text = this.GetColumnText(dbInterpreter, table, column);
                    bool isPrimaryKey = schemaInfo.TablePrimaryKeys.Any(item => item.Columns.Any(t => t.ColumnName == column.Name));
                    bool isForeignKey = schemaInfo.TableForeignKeys.Any(item => item.Columns.Any(t => t.ColumnName == column.Name));
                    string imageKeyName = isPrimaryKey ? nameof(TablePrimaryKey) : (isForeignKey ? nameof(TableForeignKey) : nameof(TableColumn));

                    var node = DbObjectsAntdTreeHelper.CreateTreeItem(column.Name, text, imageKeyName);
                    node.Tag = column;

                    treeItem.Sub.Add(node);
                }
            }
            #endregion

            if (itemName == nameof(DbObjectTreeFolderType.Triggers))
            {
                treeItem.AddDbObjectItems(schemaInfo.TableTriggers);
            }

            if (!isForView)
            {
                #region Indexes
                if (itemName == nameof(DbObjectTreeFolderType.Indexes) && schemaInfo.TableIndexes.Any())
                {
                    foreach (TableIndex index in schemaInfo.TableIndexes)
                    {
                        bool isUnique = index.IsUnique;
                        string strColumns = string.Join(",", index.Columns.OrderBy(item => item.Order).Select(item => item.ColumnName));

                        string content = index.Columns.Count > 0 ? (isUnique ? $"(Unique, {strColumns})" : $"({strColumns})")
                                        : (isUnique ? "(Unique)" : "");

                        string text = $"{index.Name}{content}";

                        var node = DbObjectsAntdTreeHelper.CreateTreeItem(index.Name, text, nameof(TableIndex));
                        node.Tag = index;

                        treeItem.Sub.Add(node);
                    }
                }
                #endregion

                if (itemName == nameof(DbObjectTreeFolderType.Keys))
                {
                    foreach (TablePrimaryKey key in schemaInfo.TablePrimaryKeys)
                    {
                        var node = DbObjectsAntdTreeHelper.CreateTreeItem(key);

                        if (string.IsNullOrEmpty(node.Text))
                        {
                            string defaultName = SchemaInfoHelper.GetTableObjectDefaultName(key);

                            node.Text = $"{defaultName}(unnamed)";
                        }

                        treeItem.Sub.Add(node);
                    }

                    foreach (TableForeignKey key in schemaInfo.TableForeignKeys)
                    {
                        var node = DbObjectsAntdTreeHelper.CreateTreeItem(key);

                        if (string.IsNullOrEmpty(node.Text))
                        {
                            string defaultName = SchemaInfoHelper.GetTableObjectDefaultName(key);

                            node.Text = $"{defaultName}(unnamed)";
                        }

                        treeItem.Sub.Add(node);
                    }
                }

                #region Constraints
                if (itemName == nameof(DbObjectTreeFolderType.Constraints) && schemaInfo.TableConstraints.Any())
                {
                    foreach (TableConstraint constraint in schemaInfo.TableConstraints)
                    {
                        var node = DbObjectsAntdTreeHelper.CreateTreeItem(constraint);
                        treeItem.Sub.Add(node);
                    }
                }
                #endregion
            }

            this.Feedback("");
        }

        private string GetColumnText(DbInterpreter dbInterpreter, Table table, TableColumn column)
        {
            string text = dbInterpreter.ParseColumn(table, column);

            if (dbInterpreter.QuotationLeftChar.HasValue)
            {
                text = text.Replace(dbInterpreter.QuotationLeftChar.ToString(), "").Replace(dbInterpreter.QuotationRightChar.ToString(), "");
            }

            int index = text.IndexOf(column.Name);

            string displayText = text.Substring(index + column.Name.Length);

            return $"{column.Name} ({displayText.ToLower().Trim()})";
        }

        private void ClearItems(AntdUI.TreeItem item)
        {
            item.Sub.Clear();
        }

        private void ShowLoading(AntdUI.TreeItem item)
        {
            string loadingText = "loading...";

            if (this.IsOnlyHasFakeChild(item))
            {
                item.Sub[0].Text = loadingText;
            }
            else
            {
                var loadingItem = DbObjectsAntdTreeHelper.CreateTreeItem("Loading", loadingText, "Loading");
                item.Sub.Add(loadingItem);
            }
        }

        private async Task LoadChildItems(AntdUI.TreeItem item)
        {
            this.ShowLoading(item);

            object tag = item.Tag;

            if (tag is Database)
            {
                Database database = tag as Database;

                this.AddDatabaseFakeItems(item, database);
            }
            else if (tag is Table)
            {
                Table table = tag as Table;
                this.AddTableFakeItems(item, table);
            }
            else if (tag is View)
            {
                View view = tag as View;
                this.AddViewFakeItems(item, view);
            }
            else if (tag == null)
            {
                string name = item.Name;

                var parentItem = GetParentItem(item);

                if (parentItem.Tag is Database)
                {
                    string databaseName = parentItem.Name;

                    DatabaseObjectType databaseObjectType = DbObjectsAntdTreeHelper.GetDbObjectTypeByFolderName(name);

                    if (databaseObjectType != DatabaseObjectType.None)
                    {
                        await this.AddDbObjectItems(item, databaseName, databaseObjectType, false);

                        this.ShowChildrenCount(item);
                    }
                }
                else if (parentItem.Tag is Table)
                {
                    DatabaseObjectType databaseObjectType = this.GetDatabaseObjectTypeByFolderNames(name);

                    await this.AddTableObjectItems(item, parentItem.Tag as Table, databaseObjectType);
                }
                else if (parentItem.Tag is View)
                {
                    Table table = ObjectHelper.CloneObject<Table>(parentItem.Tag as View);

                    DatabaseObjectType databaseObjectType = this.GetDatabaseObjectTypeByFolderNames(name);

                    await this.AddTableObjectItems(item, table, databaseObjectType, true);
                }
            }
        }

        private DatabaseObjectType GetDatabaseObjectTypeByFolderNames(string folderName)
        {
            DatabaseObjectType databaseObjectType = DatabaseObjectType.None;

            switch (folderName)
            {
                case nameof(DbObjectTreeFolderType.Columns):
                    databaseObjectType = DatabaseObjectType.Column | DatabaseObjectType.PrimaryKey | DatabaseObjectType.ForeignKey;
                    break;
                case nameof(DbObjectTreeFolderType.Triggers):
                    databaseObjectType = DatabaseObjectType.Trigger;
                    break;
                case nameof(DbObjectTreeFolderType.Indexes):
                    databaseObjectType = DatabaseObjectType.Index;
                    break;
                case nameof(DbObjectTreeFolderType.Keys):
                    databaseObjectType = DatabaseObjectType.PrimaryKey | DatabaseObjectType.ForeignKey;
                    break;
                case nameof(DbObjectTreeFolderType.Constraints):
                    databaseObjectType = DatabaseObjectType.Constraint;
                    break;
            }

            return databaseObjectType;
        }

        private void ShowChildrenCount(AntdUI.TreeItem item)
        {
            item.Text = $"{item.Name} ({item.Sub.Count})";
        }

        private async void tsmiRefresh_Click(object sender, EventArgs e)
        {
            await this.RefreshItem();
        }

        private async Task RefreshItem()
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            if (this.CanRefresh(item))
            {
                await this.LoadChildItems(item);
            }
        }

        private bool IsValidSelectedItem()
        {
            var item = this.GetSelectedItem();

            return item != null;
        }

        private AntdUI.TreeItem GetSelectedItem()
        {
            return this.tvDbObjects.SelectItem;
        }

        private async void GenerateScripts(ScriptAction scriptAction)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            await this.GenerateScripts(item, scriptAction);
        }

        private async Task GenerateScripts(AntdUI.TreeItem item, ScriptAction scriptAction)
        {
            object tag = item.Tag;

            if (tag is Database)
            {
                Database database = tag as Database;

                frmGenerateScripts frmGenerateScripts = new frmGenerateScripts(this.databaseType, this.GetConnectionInfo(database.Name));
                frmGenerateScripts.ShowDialog();
            }
            else if (tag is DatabaseObject)
            {
                string databaseName = this.GetDatabaseItem(item).Name;

                await this.GenerateObjectScript(databaseName, tag as DatabaseObject, scriptAction);
            }
        }

        private async Task GenerateObjectScript(string database, DatabaseObject dbObj, ScriptAction scriptAction)
        {
            try
            {
                DbInterpreter dbInterpreter = this.GetDbInterpreter(database, false);

                dbInterpreter.Option.ThrowExceptionWhenErrorOccurs = true;

                ScriptGenerator scriptGenerator = new ScriptGenerator(dbInterpreter);

                ScriptGenerateResult result = await scriptGenerator.Generate(dbObj, scriptAction);

                this.ShowContent(new DatabaseObjectDisplayInfo()
                {
                    Name = dbObj.Name,
                    DatabaseType = this.databaseType,
                    DatabaseObject = dbObj,
                    ConnectionInfo = dbInterpreter.ConnectionInfo,
                    Content = result.Script,
                    ScriptAction = scriptAction,
                    ScriptParameters = result.Parameters
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
            }
        }

        private void tsmiConvert_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            this.ConvertDatabase(item);
        }

        private void ConvertDatabase(AntdUI.TreeItem item)
        {
            Database database = item.Tag as Database;

            frmConvert frmConvert = new frmConvert(this.databaseType, this.GetConnectionInfo(database.Name));
            frmConvert.ShowDialog();
        }

        private async void tsmiClearData_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            if (MessageBox.Show($"Are you sure to clear all data of the database?{Environment.NewLine}Please handle this operation carefully!", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var item = this.GetSelectedItem();

                await this.ClearData(item.Name);
            }
        }

        private async Task ClearData(string database)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);
            DbManager dbManager = new DbManager(dbInterpreter);

            dbManager.Subscribe(this);
            dbInterpreter.Subscribe(this);

            bool success = await dbManager.ClearData();

            if (success)
            {
                MessageBox.Show("Data has been cleared.");
            }
        }

        private void Feedback(FeedbackInfo info)
        {
            if (this.OnFeedback != null)
            {
                this.OnFeedback(info);
            }
        }

        private void Feedback(string message)
        {
            this.Feedback(new FeedbackInfo() { Message = message });
        }

        private async void tsmiEmptyDatabase_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            if (MessageBox.Show($"Are you sure to delete all objects of the database?{Environment.NewLine}Please handle this operation carefully!", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                var dbInterpreter = this.GetDbInterpreter((this.GetSelectedItem().Tag as Database).Name, true);

                frmItemsSelector selector = new frmItemsSelector("Select Database Object Types", ItemsSelectorHelper.GetDatabaseObjectTypeItems(this.databaseType, dbInterpreter.SupportDbObjectType, true));

                if (selector.ShowDialog() == DialogResult.OK)
                {
                    var item = this.GetSelectedItem();

                    await this.EmptyDatabase(item.Name, ItemsSelectorHelper.GetDatabaseObjectTypeByCheckItems(selector.CheckedItem));

                    await this.LoadChildItems(item);

                    this.Feedback("");
                }
            }
        }

        private async Task EmptyDatabase(string database, DatabaseObjectType databaseObjectType)
        {
            DbInterpreter dbInterpreter = this.GetDbInterpreter(database);
            dbInterpreter.Option.ThrowExceptionWhenErrorOccurs = false;
            DbManager dbManager = new DbManager(dbInterpreter);

            dbInterpreter.Subscribe(this);
            dbManager.Subscribe(this);

            bool success = await dbManager.EmptyDatabase(databaseObjectType);

            if (success)
            {
                MessageBox.Show("Seleted database objects have been deleted.");
            }
        }

        private async void tvDbObjects_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                await this.RefreshItem();
            }
            else if (e.KeyCode == Keys.Delete)
            {
                this.DeleteItem();
            }

            if (e.Control)
            {
                if (e.KeyCode == Keys.F)
                {
                    this.FindChild();
                }
                else if (e.KeyCode == Keys.C)
                {
                    this.CopyItemText();
                }
            }
        }

        private void CopyItemText()
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            Clipboard.SetDataObject(item.Text);
        }

        private bool IsEmptyTreeItem(AntdUI.TreeItem item)
        {
            return item.Sub.Count == 0 || (item.Sub.Count == 1 && item.Sub[0].Tag == null);
        }

        private bool IsTreeItemHasDbObjectChildren(AntdUI.TreeItem item)
        {
            return !(this.IsEmptyTreeItem(item) || item.Sub.All(subItem => subItem.Tag == null && this.IsEmptyTreeItem(subItem)));
        }

        private void FindChild()
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();
            int level = GetItemLevel(item);

            if (level >= 1)
            {
                var parentItem = GetParentItem(item);
                var targetItems = this.IsTreeItemHasDbObjectChildren(item) ? item.Sub : parentItem.Sub;

                if (targetItems.Count <= 1)
                {
                    return;
                }

                frmFindBox findBox = new frmFindBox();

                findBox.StartPosition = FormStartPosition.Manual;
                findBox.Location = new System.Drawing.Point(0, 90);

                DialogResult result = findBox.ShowDialog();

                if (result == DialogResult.OK)
                {
                    string word = findBox.FindWord;

                    var foundItem = this.FindTreeItem(targetItems, word);

                    if (foundItem != null)
                    {
                        this.tvDbObjects.SelectItem = foundItem;
                    }
                    else
                    {
                        MessageBox.Show("Not found.");
                    }
                }
            }
        }

        private AntdUI.TreeItem FindTreeItem(AntdUI.TreeItemCollection items, string word)
        {
            foreach (var item in items)
            {
                if (item.Tag != null)
                {
                    string text = item.Text.Split('.').LastOrDefault()?.Split('(')?.FirstOrDefault()?.Trim();

                    if (text.ToUpper() == word.ToUpper())
                    {
                        return item;
                    }
                }
                else if (item.Sub.Count >= 1)
                {
                    return this.FindTreeItem(item.Sub, word);
                }
            }

            return null;
        }

        private void DeleteItem()
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            if (this.CanDelete(item))
            {
                if (MessageBox.Show("Are you sure to delete this object?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.DropDbObject(item);
                }
            }
        }

        private async void DropDbObject(AntdUI.TreeItem item)
        {
            string database = this.GetDatabaseItem(item).Name;
            DatabaseObject dbObject = item.Tag as DatabaseObject;

            DbInterpreter dbInterpreter = this.GetDbInterpreter(database, true, true);
            dbInterpreter.Subscribe(this);

            DbManager dbManager = new DbManager(dbInterpreter);

            dbInterpreter.Subscribe(this);
            dbManager.Subscribe(this);

            bool success = await dbManager.DropDbObject(dbObject);

            if (success)
            {
                var parentItem = GetParentItem(item);
                bool parentIsChildFolderOfDatabase = GetItemLevel(item) == 3;

                parentItem.Sub.Remove(item);

                if (parentIsChildFolderOfDatabase)
                {
                    this.ShowChildrenCount(parentItem);
                }
            }
            else
            {
            }
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            this.DeleteItem();
        }

        private void tsmiViewData_Click(object sender, EventArgs e)
        {
            this.ProcessData(DatabaseObjectDisplayType.ViewData);
        }

        private void tsmiEditData_Click(object sender, EventArgs e)
        {
            this.ProcessData(DatabaseObjectDisplayType.EditData);
        }

        private void ProcessData(DatabaseObjectDisplayType type)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            string database = this.GetDatabaseItem(item).Name;
            DatabaseObject dbObject = item.Tag as DatabaseObject;

            this.ShowContent(new DatabaseObjectDisplayInfo() { Name = dbObject.Name, Schema = dbObject.Schema, DatabaseType = this.databaseType, DatabaseObject = dbObject, DisplayType = type, ConnectionInfo = this.GetConnectionInfo(database) });
        }

        public void OnNext(FeedbackInfo value)
        {
            this.Feedback(value);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        private void tsmiTranslate_MouseEnter(object sender, EventArgs e)
        {
            this.tsmiTranslate.DropDownItems.Clear();

            var item = this.GetSelectedItem();

            if (item == null || item.Tag == null)
            {
                return;
            }

            DatabaseObjectType dbObjectType = DbObjectHelper.GetDatabaseObjectType(item.Tag as DatabaseObject);

            var dbTypes = DbInterpreterHelper.GetDisplayDatabaseTypes();

            foreach (var dbType in dbTypes)
            {
                if ((int)dbType != (int)this.databaseType)
                {
                    var dbInterpreter = DbInterpreterHelper.GetDbInterpreter(dbType, new ConnectionInfo(), new DbInterpreterOption());

                    if (dbInterpreter.SupportDbObjectType.HasFlag(dbObjectType))
                    {
                        ToolStripMenuItem menuItem = new ToolStripMenuItem(dbType.ToString());
                        menuItem.Click += TranslateItem_Click;

                        this.tsmiTranslate.DropDownItems.Add(menuItem);
                    }
                }
            }
        }

        private async void TranslateItem_Click(object sender, EventArgs e)
        {
            DatabaseType dbType = ManagerUtil.GetDatabaseType((sender as ToolStripMenuItem).Text);

            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();
            this.tvDbObjects.SelectItem = item;

            await this.Translate(item, dbType);
        }

        private async Task Translate(AntdUI.TreeItem item, DatabaseType targetDbType)
        {
            object tag = item.Tag;

            ConnectionInfo connectionInfo = this.GetConnectionInfo((this.GetDatabaseItem(item).Tag as Database).Name);

            if (tag is DatabaseObject)
            {
                TranslateManager translateManager = new TranslateManager();
                translateManager.Subscribe(this);

                var dbObject = tag as DatabaseObject;

                try
                {
                    TranslateResult result = await translateManager.Translate(this.databaseType, targetDbType, dbObject, connectionInfo, true);

                    if (result != null)
                    {
                        DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo()
                        {
                            Schema = result.DbObjectSchema ?? dbObject.Schema,
                            Name = dbObject.Name,
                            DatabaseType = targetDbType,
                            DatabaseObject = dbObject,
                            Content = result.Data?.ToString(),
                            ConnectionInfo = null,
                            Error = result.Error,
                            IsTranlatedScript = true
                        };

                        this.ShowContent(info);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ExceptionHelper.GetExceptionDetails(ex));
                }
            }
        }

        public DatabaseObjectDisplayInfo GetDisplayInfo()
        {
            var item = this.tvDbObjects.SelectItem;

            DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo() { DatabaseType = this.DatabaseType };

            if (item != null)
            {
                if (item.Tag is DatabaseObject dbObject)
                {
                    info.Name = dbObject.Name;
                    info.DatabaseObject = dbObject;
                }

                var databaseItem = this.GetDatabaseItem(item);

                if (databaseItem != null)
                {
                    info.ConnectionInfo = this.GetConnectionInfo(databaseItem.Name);
                }
                else
                {
                    info.ConnectionInfo = this.connectionInfo;
                }
            }

            return info;
        }

        private void tsmiNewQuery_Click(object sender, EventArgs e)
        {
            this.ShowContent(DatabaseObjectDisplayType.Script);
        }

        private void ShowContent(DatabaseObjectDisplayInfo info)
        {
            if (this.OnShowContent != null)
            {
                this.OnShowContent(info);
            }
        }

        private void ShowContent(DatabaseObjectDisplayType displayType, bool isNew = true)
        {
            DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo() { IsNew = isNew, DisplayType = displayType, DatabaseType = this.DatabaseType };

            if (!isNew)
            {
                DatabaseObject dbObject = this.tvDbObjects.SelectItem?.Tag as DatabaseObject;

                if (dbObject != null)
                {
                    info.DatabaseObject = dbObject;
                    info.Schema = dbObject.Schema;
                    info.Name = dbObject.Name;
                }
            }

            info.ConnectionInfo = this.GetCurrentConnectionInfo();

            this.ShowContent(info);
        }

        private void tsmiNewView_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.View, ScriptAction.CREATE);
        }

        private void tsmiNewFunction_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.Function, ScriptAction.CREATE);
        }

        private void tsmiNewProcedure_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.Procedure, ScriptAction.CREATE);
        }

        private void tsmiNewTrigger_Click(object sender, EventArgs e)
        {
            this.DoScript(DatabaseObjectType.Trigger, ScriptAction.CREATE);
        }

        private void DoScript(DatabaseObjectType databaseObjectType, ScriptAction scriptAction)
        {
            DbInterpreter dbInterpreter = DbInterpreterHelper.GetDbInterpreter(this.databaseType, new ConnectionInfo(), new DbInterpreterOption());

            ScriptTemplate scriptTemplate = new ScriptTemplate(dbInterpreter);

            DatabaseObjectDisplayInfo displayInfo = this.GetDisplayInfo();
            displayInfo.IsNew = true;

            DatabaseObject dbObj = null;

            if (databaseObjectType == DatabaseObjectType.Trigger)
            {
                dbObj = GetParentItem(this.GetSelectedItem())?.Tag as Table;
            }

            displayInfo.Content = scriptTemplate.GetTemplateContent(databaseObjectType, scriptAction, dbObj);
            displayInfo.ScriptAction = scriptAction;

            this.ShowContent(displayInfo);
        }

        private void tsmiAlter_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.ALTER);
        }

        private void tsmiNewTable_Click(object sender, EventArgs e)
        {
            this.ShowContent(DatabaseObjectDisplayType.TableDesigner);
        }

        private void tsmiDesign_Click(object sender, EventArgs e)
        {
            this.ShowContent(DatabaseObjectDisplayType.TableDesigner, false);
        }

        private void tvDbObjects_NodeMouseDoubleClick(object sender, EventArgs e)
        {
            var item = this.tvDbObjects.SelectItem;
            if (item == null || item.Tag == null) return;

            bool isTable = item.Tag is Table;
            bool isView = item.Tag is View;
            bool isProcedure = item.Tag is Procedure;
            bool isFunction = item.Tag is Function;

            if (isTable || isView)
            {
                this.ShowContent(DatabaseObjectDisplayType.ViewData, false);
            }
            else if (isFunction || isProcedure)
            {
                this.DoScript(item.Tag is Function ? DatabaseObjectType.Function : DatabaseObjectType.Procedure, ScriptAction.CREATE);
            }
            else if (item.Tag is TableTrigger)
            {
                this.tsmiAlter_Click(sender, e);
            }
        }

        private void tsmiNewColumn_Click(object sender, EventArgs e)
        {
            this.ShowTableDesignerForColumns(isModifyColumn: false);
        }

        private void tsmiModifyColumn_Click(object sender, EventArgs e)
        {
            this.ShowTableDesignerForColumns(isModifyColumn: true);
        }

        private void ShowTableDesignerForColumns(bool isModifyColumn)
        {
            var item = this.tvDbObjects.SelectItem;
            if (item == null)
            {
                return;
            }

            var tableItem = item;
            while (tableItem != null && !(tableItem.Tag is Table))
            {
                tableItem = GetParentItem(tableItem);
            }

            if (tableItem != null)
            {
                this.tvDbObjects.SelectItem = tableItem;

                DatabaseObjectDisplayInfo info = new DatabaseObjectDisplayInfo()
                {
                    IsNew = false,
                    DisplayType = DatabaseObjectDisplayType.TableDesigner,
                    DatabaseType = this.DatabaseType,
                    ConnectionInfo = this.GetCurrentConnectionInfo()
                };

                Table table = tableItem.Tag as Table;
                if (table == null)
                {
                    return;
                }

                info.DatabaseObject = table;
                info.Schema = table.Schema;
                info.Name = table.Name;

                if (isModifyColumn && item.Tag is TableColumn column)
                {
                    info.Content = $"MODIFY_COLUMN:{column.Name}";
                }
                else
                {
                    info.Content = "NEW_COLUMN";
                }

                this.ShowContent(info);
            }
        }

        private async void RefreshFolderNode()
        {
            var item = this.tvDbObjects.SelectItem;

            if (item == null)
            {
                return;
            }

            if (item.Tag is DatabaseObject && GetParentItem(item) != null && this.CanRefresh(GetParentItem(item)))
            {
                string selectedName = item.Name;

                var parentItem = GetParentItem(item);

                await this.LoadChildItems(parentItem);

                foreach (var child in parentItem.Sub)
                {
                    if (child.Name == selectedName)
                    {
                        this.tvDbObjects.SelectItem = child;
                        break;
                    }
                }

                this.tvDbObjects.SelectItem = parentItem;
            }
            else if (!(item.Tag is DatabaseObject) && this.CanRefresh(item))
            {
                this.tvDbObjects.SelectItem = item;

                await this.LoadChildItems(item);
            }
        }

        private async void tsmiBackup_Click(object sender, EventArgs e)
        {
            ConnectionInfo connectionInfo = this.GetCurrentConnectionInfo();

            DbManager dbManager = new DbManager();

            dbManager.Subscribe(this);

            Action<BackupSetting> backup = (setting) =>
            {
                bool success = dbManager.Backup(setting, connectionInfo);

                if (success)
                {
                    MessageBox.Show("Backup finished.");
                }
            };

            frmBackupSettingRedefine form = new frmBackupSettingRedefine() { DatabaseType = this.databaseType };

            if (form.ShowDialog() == DialogResult.OK)
            {
                await Task.Run(() => backup(form.Setting));
            }
        }

        private void tsmiDiagnose_Click(object sender, EventArgs e)
        {
            ConnectionInfo connectionInfo = this.GetCurrentConnectionInfo();

            string schema = null;

            if (this.databaseType == DatabaseType.Oracle)
            {
                schema = this.GetDatabaseItem(this.GetSelectedItem()).Name;
            }

            frmDiagnose form = new frmDiagnose(databaseType, connectionInfo, schema);

            form.Subscribe(this);
            form.ShowDialog();
        }

        private void tsmiCopy_Click(object sender, EventArgs e)
        {
            frmTableCopy form = new frmTableCopy()
            {
                DatabaseType = this.databaseType,
                ConnectionInfo = this.GetCurrentConnectionInfo(),
                Table = this.tvDbObjects.SelectItem?.Tag as Table
            };

            form.OnFeedback += this.Feedback;

            form.Show();
        }

        private void tsmiCompareSchema_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            this.CompareSchema(item);
        }

        private void CompareSchema(AntdUI.TreeItem item)
        {
            Database database = item.Tag as Database;

            frmSchemaCompare frmCompare = new frmSchemaCompare(this.databaseType, this.GetConnectionInfo(database.Name));
            frmCompare.ShowDialog();
        }

        private void tsmiCreateScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.CREATE);
        }

        private void tsmiSelectScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.SELECT);
        }

        private void tsmiInsertScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.INSERT);
        }

        private void tsmiUpdateScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.UPDATE);
        }

        private void tsmiDeleteScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.DELETE);
        }

        private void tsmiViewDependency_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            var tag = item.Tag;
            Database database = null;

            if (tag is Database)
            {
                database = tag as Database;

                frmTableDependency tableDependency = new frmTableDependency(this.databaseType, this.GetConnectionInfo(database.Name), null);

                tableDependency.Show();
            }
            else if (tag is DatabaseObject dbObj)
            {
                database = this.GetDatabaseItem(item).Tag as Database;

                frmDbObjectDependency dbOjectDependency = new frmDbObjectDependency(this.databaseType, this.GetConnectionInfo(database.Name), dbObj);

                dbOjectDependency.Show();
            }
        }

        private void tsmiCopyChildrenNames_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            if (item != null)
            {
                var dbObjects = item.Sub.Select(subItem => subItem.Tag as DatabaseObject).Where(x => x != null);

                bool showSchema = this.NeedShowSchema(item, dbObjects);

                var names = dbObjects.Select(dbObj => (showSchema ? $"{dbObj.Schema}.{dbObj.Name}" : dbObj.Name));

                string content = string.Join(Environment.NewLine, names);

                frmTextContent frm = new frmTextContent(content);
                frm.Show();
            }
        }

        private void tsmiExecuteScript_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.EXECUTE);
        }

        private void tsmiStatistic_Click(object sender, EventArgs e)
        {
            ConnectionInfo connectionInfo = this.GetCurrentConnectionInfo();

            string schema = null;

            if (this.databaseType == DatabaseType.Oracle)
            {
                schema = this.GetDatabaseItem(this.GetSelectedItem()).Name;
            }

            frmStatistic form = new frmStatistic(this.databaseType, connectionInfo, schema);

            form.Subscribe(this);

            form.ShowDialog();
        }

        private void tsmiExportData_Click(object sender, EventArgs e)
        {
            this.ExportData();
        }

        private void ExportData()
        {
            var item = this.GetSelectedItem();

            DatabaseObject tableOrView = item.Tag as DatabaseObject;

            var dbInterpreter = this.GetDbInterpreter(this.GetDatabaseItem(item).Name, true);

            frmExportData frm = new frmExportData(dbInterpreter, tableOrView, null, false);

            frm.OnFeedback += this.Feedback;

            frm.Show();
        }

        private void tsmiImportData_Click(object sender, EventArgs e)
        {
            this.ImportData();
        }

        private void ImportData()
        {
            var item = this.GetSelectedItem();

            Table table = item.Tag as Table;

            var dbInterpreter = this.GetDbInterpreter(this.GetDatabaseItem(item).Name, true);

            frmImportData frm = new frmImportData(dbInterpreter, table);

            DialogResult result = frm.ShowDialog();
        }

        private void tsmiCompareData_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            this.CompareData(item);
        }

        private void CompareData(AntdUI.TreeItem item)
        {
            Database database = item.Tag as Database;

            frmDataCompare frm = new frmDataCompare(this.databaseType, this.GetConnectionInfo(database.Name));
            frm.ShowDialog();
        }

        private void tsmiGenerateColumnDocumentation_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            this.GenerateColumnDocumentation(item);
        }

        private void GenerateColumnDocumentation(AntdUI.TreeItem item)
        {
            Database database = item.Tag as Database;

            frmGenerateColumnDocumentation frm = new frmGenerateColumnDocumentation(this.databaseType, this.GetConnectionInfo(database.Name));
            frm.ShowDialog();
        }

        private void tsmiDatabaseDiagram_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            Database database = item.Tag as Database;

            DbInterpreter dbInterpreter = this.GetDbInterpreter(database.Name, false, true);

            frmDatabaseDiagram frm = new frmDatabaseDiagram(dbInterpreter);
            frm.ShowDialog();
        }

        private void tsmiGenerateInsertProcedure_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.CREATE_PROCEDURE_INSERT);
        }

        private void tsmiUpdateProcedure_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.CREATE_PROCEDURE_UPDATE);
        }

        private void tsmiDeleteProcedure_Click(object sender, EventArgs e)
        {
            this.GenerateScripts(ScriptAction.CREATE_PROCEDURE_DELETE);
        }

        public bool HasSelectedNode()
        {
            return this.tvDbObjects.SelectItem != null;
        }

        public void SelectNone()
        {
            this.tvDbObjects.SelectItem = null;
        }

        private void tsmiIndexFragmentation_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            Database database = item.Tag as Database;

            frmIndexFragmentation frm = new frmIndexFragmentation(this.GetDbInterpreter(database.Name, true, true));
            frm.Show();
        }

        private async void tsmiOptimize_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var confirmResult = MessageBox.Show("Are you sure to optimize the database?", "Confirm", MessageBoxButtons.YesNo);

            if (confirmResult != DialogResult.Yes)
            {
                return;
            }

            var item = this.GetSelectedItem();

            Database database = item.Tag as Database;

            Optimizer optimizer = new Optimizer(this.GetDbInterpreter(database.Name, true, true));

            this.Feedback("Start to optimize...");

            var result = await optimizer.Optimize();

            this.Feedback("End optimize.");

            if (result.IsOK)
            {
                frmOpitimizeResult frm = new frmOpitimizeResult(result);
                frm.Show();
            }
            else
            {
                MessageBox.Show(result.Message);
            }
        }

        private void tsmiDisconnect_Click(object sender, EventArgs e)
        {
            if (!this.IsValidSelectedItem())
            {
                return;
            }

            var item = this.GetSelectedItem();

            if (item.Tag is Database)
            {
                if (MessageBox.Show("Are you sure to disconnect from this database?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    this.tvDbObjects.Items.Remove(item);
                    FormEventCenter.OnDatabaseDisconnected?.Invoke(this.connectionInfo);
                }
            }
        }
    }
}
