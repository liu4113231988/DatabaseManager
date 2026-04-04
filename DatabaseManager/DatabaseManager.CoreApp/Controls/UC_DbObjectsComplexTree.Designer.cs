namespace DatabaseManager.Controls
{
    partial class UC_DbObjectsComplexTree
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            tvDbObjects = new AntdUI.Tree();
            contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(components);
            tsmiNewQuery = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewTable = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewView = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewFunction = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewProcedure = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewTrigger = new System.Windows.Forms.ToolStripMenuItem();
            tsmiAlter = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDesign = new System.Windows.Forms.ToolStripMenuItem();
            tsmiRefresh = new System.Windows.Forms.ToolStripMenuItem();
            tsmiViewData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiEditData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiExportData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiImportData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiConvert = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCompareSchema = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCompareData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiGenerateScripts = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCreateScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiSelectScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiInsertScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiUpdateScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDeleteScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiGenerateProcedure = new System.Windows.Forms.ToolStripMenuItem();
            tsmiGenerateInsertProcedure = new System.Windows.Forms.ToolStripMenuItem();
            tsmiUpdateProcedure = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDeleteProcedure = new System.Windows.Forms.ToolStripMenuItem();
            tsmiExecuteScript = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDisconnect = new System.Windows.Forms.ToolStripMenuItem();
            tsmiTranslate = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCopy = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDelete = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDatabaseDiagram = new System.Windows.Forms.ToolStripMenuItem();
            tsmiViewDependency = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCopyChildrenNames = new System.Windows.Forms.ToolStripMenuItem();
            tsmiMore = new System.Windows.Forms.ToolStripMenuItem();
            tsmiBackup = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDiagnose = new System.Windows.Forms.ToolStripMenuItem();
            tsmiStatistic = new System.Windows.Forms.ToolStripMenuItem();
            tsmiAnalysis = new System.Windows.Forms.ToolStripMenuItem();
            tsmiIndexFragmentation = new System.Windows.Forms.ToolStripMenuItem();
            tsmiOptimize = new System.Windows.Forms.ToolStripMenuItem();
            tsmiClearData = new System.Windows.Forms.ToolStripMenuItem();
            tsmiEmptyDatabase = new System.Windows.Forms.ToolStripMenuItem();
            documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            tsmiGenerateColumnDocumentation = new System.Windows.Forms.ToolStripMenuItem();
            tsmiNewColumn = new System.Windows.Forms.ToolStripMenuItem();
            tsmiModifyColumn = new System.Windows.Forms.ToolStripMenuItem();
            contextMenuStrip1.SuspendLayout();
            SuspendLayout();

            tvDbObjects.Dock = System.Windows.Forms.DockStyle.Fill;
            tvDbObjects.Location = new System.Drawing.Point(0, 0);
            tvDbObjects.Name = "tvDbObjects";
            tvDbObjects.Size = new System.Drawing.Size(186, 276);
            tvDbObjects.TabIndex = 20;
            tvDbObjects.BackHover = System.Drawing.Color.FromArgb(245, 250, 255);
            tvDbObjects.BackActive = System.Drawing.Color.FromArgb(230, 240, 255);
            tvDbObjects.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);
            tvDbObjects.ForeActive = System.Drawing.Color.FromArgb(0, 80, 160);
            tvDbObjects.Radius = 6;
            tvDbObjects.Gap = 6;
            tvDbObjects.IconRatio = 1.5F;
            tvDbObjects.ContextMenuStrip = contextMenuStrip1;

            contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiNewQuery, tsmiNewTable, tsmiNewView, tsmiNewFunction, tsmiNewProcedure, tsmiNewTrigger, tsmiAlter, tsmiDesign, tsmiNewColumn, tsmiModifyColumn, tsmiRefresh, tsmiViewData, tsmiEditData, tsmiExportData, tsmiImportData, tsmiConvert, tsmiCompareSchema, tsmiCompareData, tsmiGenerateScripts, tsmiTranslate, tsmiCopy, tsmiDelete, tsmiDatabaseDiagram, tsmiViewDependency, tsmiCopyChildrenNames, tsmiMore, tsmiDisconnect });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(204, 532);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;

            tsmiNewQuery.Name = "tsmiNewQuery";
            tsmiNewQuery.Size = new System.Drawing.Size(203, 22);
            tsmiNewQuery.Text = "New Query";
            tsmiNewQuery.Click += tsmiNewQuery_Click;

            tsmiNewTable.Name = "tsmiNewTable";
            tsmiNewTable.Size = new System.Drawing.Size(203, 22);
            tsmiNewTable.Text = "New Table";
            tsmiNewTable.Click += tsmiNewTable_Click;

            tsmiNewView.Name = "tsmiNewView";
            tsmiNewView.Size = new System.Drawing.Size(203, 22);
            tsmiNewView.Text = "New View";
            tsmiNewView.Click += tsmiNewView_Click;

            tsmiNewFunction.Name = "tsmiNewFunction";
            tsmiNewFunction.Size = new System.Drawing.Size(203, 22);
            tsmiNewFunction.Text = "New Function";
            tsmiNewFunction.Click += tsmiNewFunction_Click;

            tsmiNewProcedure.Name = "tsmiNewProcedure";
            tsmiNewProcedure.Size = new System.Drawing.Size(203, 22);
            tsmiNewProcedure.Text = "New Procedure";
            tsmiNewProcedure.Click += tsmiNewProcedure_Click;

            tsmiNewTrigger.Name = "tsmiNewTrigger";
            tsmiNewTrigger.Size = new System.Drawing.Size(203, 22);
            tsmiNewTrigger.Text = "New Trigger";
            tsmiNewTrigger.Click += tsmiNewTrigger_Click;

            tsmiAlter.Name = "tsmiAlter";
            tsmiAlter.Size = new System.Drawing.Size(203, 22);
            tsmiAlter.Text = "Modify";
            tsmiAlter.Click += tsmiAlter_Click;

            tsmiDesign.Name = "tsmiDesign";
            tsmiDesign.Size = new System.Drawing.Size(203, 22);
            tsmiDesign.Text = "Design";
            tsmiDesign.Click += tsmiDesign_Click;

            tsmiRefresh.Name = "tsmiRefresh";
            tsmiRefresh.Size = new System.Drawing.Size(203, 22);
            tsmiRefresh.Text = "Refresh";
            tsmiRefresh.Click += tsmiRefresh_Click;

            tsmiViewData.Name = "tsmiViewData";
            tsmiViewData.Size = new System.Drawing.Size(203, 22);
            tsmiViewData.Text = "View Data";
            tsmiViewData.Click += tsmiViewData_Click;

            tsmiEditData.Name = "tsmiEditData";
            tsmiEditData.Size = new System.Drawing.Size(203, 22);
            tsmiEditData.Text = "Edit Data";
            tsmiEditData.Click += tsmiEditData_Click;

            tsmiExportData.Name = "tsmiExportData";
            tsmiExportData.Size = new System.Drawing.Size(203, 22);
            tsmiExportData.Text = "Export Data";
            tsmiExportData.Click += tsmiExportData_Click;

            tsmiImportData.Name = "tsmiImportData";
            tsmiImportData.Size = new System.Drawing.Size(203, 22);
            tsmiImportData.Text = "Import Data";
            tsmiImportData.Click += tsmiImportData_Click;

            tsmiConvert.Name = "tsmiConvert";
            tsmiConvert.Size = new System.Drawing.Size(203, 22);
            tsmiConvert.Text = "Convert";
            tsmiConvert.Click += tsmiConvert_Click;

            tsmiCompareSchema.Name = "tsmiCompareSchema";
            tsmiCompareSchema.Size = new System.Drawing.Size(203, 22);
            tsmiCompareSchema.Text = "Compare Schema";
            tsmiCompareSchema.Click += tsmiCompareSchema_Click;

            tsmiCompareData.Name = "tsmiCompareData";
            tsmiCompareData.Size = new System.Drawing.Size(203, 22);
            tsmiCompareData.Text = "Compare Data";
            tsmiCompareData.Click += tsmiCompareData_Click;

            tsmiGenerateScripts.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiCreateScript, tsmiSelectScript, tsmiInsertScript, tsmiUpdateScript, tsmiDeleteScript, tsmiGenerateProcedure, tsmiExecuteScript });
            tsmiGenerateScripts.Name = "tsmiGenerateScripts";
            tsmiGenerateScripts.Size = new System.Drawing.Size(203, 22);
            tsmiGenerateScripts.Text = "Scripts";

            tsmiCreateScript.Name = "tsmiCreateScript";
            tsmiCreateScript.Size = new System.Drawing.Size(193, 22);
            tsmiCreateScript.Text = "Create Script";
            tsmiCreateScript.Click += tsmiCreateScript_Click;

            tsmiSelectScript.Name = "tsmiSelectScript";
            tsmiSelectScript.Size = new System.Drawing.Size(193, 22);
            tsmiSelectScript.Text = "Select Script";
            tsmiSelectScript.Click += tsmiSelectScript_Click;

            tsmiInsertScript.Name = "tsmiInsertScript";
            tsmiInsertScript.Size = new System.Drawing.Size(193, 22);
            tsmiInsertScript.Text = "Insert Script";
            tsmiInsertScript.Click += tsmiInsertScript_Click;

            tsmiUpdateScript.Name = "tsmiUpdateScript";
            tsmiUpdateScript.Size = new System.Drawing.Size(193, 22);
            tsmiUpdateScript.Text = "Update Script";
            tsmiUpdateScript.Click += tsmiUpdateScript_Click;

            tsmiDeleteScript.Name = "tsmiDeleteScript";
            tsmiDeleteScript.Size = new System.Drawing.Size(193, 22);
            tsmiDeleteScript.Text = "Delete Script";
            tsmiDeleteScript.Click += tsmiDeleteScript_Click;

            tsmiGenerateProcedure.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiGenerateInsertProcedure, tsmiUpdateProcedure, tsmiDeleteProcedure });
            tsmiGenerateProcedure.Name = "tsmiGenerateProcedure";
            tsmiGenerateProcedure.Size = new System.Drawing.Size(193, 22);
            tsmiGenerateProcedure.Text = "Generate Procedure";

            tsmiGenerateInsertProcedure.Name = "tsmiGenerateInsertProcedure";
            tsmiGenerateInsertProcedure.Size = new System.Drawing.Size(183, 22);
            tsmiGenerateInsertProcedure.Text = "Insert Procedure";
            tsmiGenerateInsertProcedure.Click += tsmiGenerateInsertProcedure_Click;

            tsmiUpdateProcedure.Name = "tsmiUpdateProcedure";
            tsmiUpdateProcedure.Size = new System.Drawing.Size(183, 22);
            tsmiUpdateProcedure.Text = "Update Procedure";
            tsmiUpdateProcedure.Click += tsmiUpdateProcedure_Click;

            tsmiDeleteProcedure.Name = "tsmiDeleteProcedure";
            tsmiDeleteProcedure.Size = new System.Drawing.Size(183, 22);
            tsmiDeleteProcedure.Text = "Delete Procedure";
            tsmiDeleteProcedure.Click += tsmiDeleteProcedure_Click;

            tsmiExecuteScript.Name = "tsmiExecuteScript";
            tsmiExecuteScript.Size = new System.Drawing.Size(193, 22);
            tsmiExecuteScript.Text = "Execute Script";
            tsmiExecuteScript.Click += tsmiExecuteScript_Click;

            tsmiDisconnect.Name = "tsmiDisconnect";
            tsmiDisconnect.Size = new System.Drawing.Size(203, 22);
            tsmiDisconnect.Text = "Disconnect";
            tsmiDisconnect.Click += tsmiDisconnect_Click;

            tsmiTranslate.Name = "tsmiTranslate";
            tsmiTranslate.Size = new System.Drawing.Size(203, 22);
            tsmiTranslate.Text = "Translate to";
            tsmiTranslate.MouseEnter += tsmiTranslate_MouseEnter;

            tsmiCopy.Name = "tsmiCopy";
            tsmiCopy.Size = new System.Drawing.Size(203, 22);
            tsmiCopy.Text = "Copy";
            tsmiCopy.Click += tsmiCopy_Click;

            tsmiDelete.Name = "tsmiDelete";
            tsmiDelete.Size = new System.Drawing.Size(203, 22);
            tsmiDelete.Text = "Delete";
            tsmiDelete.Click += tsmiDelete_Click;

            tsmiDatabaseDiagram.Name = "tsmiDatabaseDiagram";
            tsmiDatabaseDiagram.Size = new System.Drawing.Size(203, 22);
            tsmiDatabaseDiagram.Text = "Diagram";
            tsmiDatabaseDiagram.Click += tsmiDatabaseDiagram_Click;

            tsmiViewDependency.Name = "tsmiViewDependency";
            tsmiViewDependency.Size = new System.Drawing.Size(203, 22);
            tsmiViewDependency.Text = "View Dependencies";
            tsmiViewDependency.Click += tsmiViewDependency_Click;

            tsmiCopyChildrenNames.Name = "tsmiCopyChildrenNames";
            tsmiCopyChildrenNames.Size = new System.Drawing.Size(203, 22);
            tsmiCopyChildrenNames.Text = "Copy Children Names";
            tsmiCopyChildrenNames.Click += tsmiCopyChildrenNames_Click;

            tsmiMore.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiBackup, tsmiDiagnose, tsmiStatistic, tsmiAnalysis, tsmiOptimize, tsmiClearData, tsmiEmptyDatabase, documentationToolStripMenuItem });
            tsmiMore.Name = "tsmiMore";
            tsmiMore.Size = new System.Drawing.Size(203, 22);
            tsmiMore.Text = "More";

            tsmiBackup.Name = "tsmiBackup";
            tsmiBackup.Size = new System.Drawing.Size(164, 22);
            tsmiBackup.Text = "Backup";
            tsmiBackup.Click += tsmiBackup_Click;

            tsmiDiagnose.Name = "tsmiDiagnose";
            tsmiDiagnose.Size = new System.Drawing.Size(164, 22);
            tsmiDiagnose.Text = "Diagnose";
            tsmiDiagnose.Click += tsmiDiagnose_Click;

            tsmiStatistic.Name = "tsmiStatistic";
            tsmiStatistic.Size = new System.Drawing.Size(164, 22);
            tsmiStatistic.Text = "Statistic";
            tsmiStatistic.Click += tsmiStatistic_Click;

            tsmiAnalysis.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiIndexFragmentation });
            tsmiAnalysis.Name = "tsmiAnalysis";
            tsmiAnalysis.Size = new System.Drawing.Size(164, 22);
            tsmiAnalysis.Text = "Analysis";

            tsmiIndexFragmentation.Name = "tsmiIndexFragmentation";
            tsmiIndexFragmentation.Size = new System.Drawing.Size(196, 22);
            tsmiIndexFragmentation.Text = "Index Fragmentation";
            tsmiIndexFragmentation.Click += tsmiIndexFragmentation_Click;

            tsmiOptimize.Name = "tsmiOptimize";
            tsmiOptimize.Size = new System.Drawing.Size(164, 22);
            tsmiOptimize.Text = "Optimize";
            tsmiOptimize.Click += tsmiOptimize_Click;

            tsmiClearData.Name = "tsmiClearData";
            tsmiClearData.Size = new System.Drawing.Size(164, 22);
            tsmiClearData.Text = "Clear Data";
            tsmiClearData.Click += tsmiClearData_Click;

            tsmiEmptyDatabase.Name = "tsmiEmptyDatabase";
            tsmiEmptyDatabase.Size = new System.Drawing.Size(164, 22);
            tsmiEmptyDatabase.Text = "Empty";
            tsmiEmptyDatabase.Click += tsmiEmptyDatabase_Click;

            documentationToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiGenerateColumnDocumentation });
            documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            documentationToolStripMenuItem.Size = new System.Drawing.Size(164, 22);
            documentationToolStripMenuItem.Text = "Documentation";

            tsmiGenerateColumnDocumentation.Name = "tsmiGenerateColumnDocumentation";
            tsmiGenerateColumnDocumentation.Size = new System.Drawing.Size(266, 22);
            tsmiGenerateColumnDocumentation.Text = "Generate column documentation";
            tsmiGenerateColumnDocumentation.Click += tsmiGenerateColumnDocumentation_Click;

            tsmiNewColumn.Name = "tsmiNewColumn";
            tsmiNewColumn.Size = new System.Drawing.Size(203, 22);
            tsmiNewColumn.Text = "New Column";
            tsmiNewColumn.Click += tsmiNewColumn_Click;

            tsmiModifyColumn.Name = "tsmiModifyColumn";
            tsmiModifyColumn.Size = new System.Drawing.Size(203, 22);
            tsmiModifyColumn.Text = "Modify Column";
            tsmiModifyColumn.Click += tsmiModifyColumn_Click;

            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Controls.Add(tvDbObjects);
            Margin = new System.Windows.Forms.Padding(4);
            Name = "UC_DbObjectsComplexTree";
            Size = new System.Drawing.Size(186, 276);
            contextMenuStrip1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.Tree tvDbObjects;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem tsmiGenerateScripts;
        private System.Windows.Forms.ToolStripMenuItem tsmiRefresh;
        private System.Windows.Forms.ToolStripMenuItem tsmiConvert;
        private System.Windows.Forms.ToolStripMenuItem tsmiDelete;
        private System.Windows.Forms.ToolStripMenuItem tsmiViewData;
        private System.Windows.Forms.ToolStripMenuItem tsmiTranslate;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewQuery;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewView;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewFunction;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewProcedure;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewTrigger;
        private System.Windows.Forms.ToolStripMenuItem tsmiMore;
        private System.Windows.Forms.ToolStripMenuItem tsmiClearData;
        private System.Windows.Forms.ToolStripMenuItem tsmiEmptyDatabase;
        private System.Windows.Forms.ToolStripMenuItem tsmiAlter;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewTable;
        private System.Windows.Forms.ToolStripMenuItem tsmiDesign;
        private System.Windows.Forms.ToolStripMenuItem tsmiBackup;
        private System.Windows.Forms.ToolStripMenuItem tsmiDiagnose;
        private System.Windows.Forms.ToolStripMenuItem tsmiCopy;
        private System.Windows.Forms.ToolStripMenuItem tsmiCompareSchema;
        private System.Windows.Forms.ToolStripMenuItem tsmiCreateScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiSelectScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiInsertScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiUpdateScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiDeleteScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiViewDependency;
        private System.Windows.Forms.ToolStripMenuItem tsmiCopyChildrenNames;
        private System.Windows.Forms.ToolStripMenuItem tsmiExecuteScript;
        private System.Windows.Forms.ToolStripMenuItem tsmiDisconnect;
        private System.Windows.Forms.ToolStripMenuItem tsmiStatistic;
        private System.Windows.Forms.ToolStripMenuItem tsmiEditData;
        private System.Windows.Forms.ToolStripMenuItem tsmiExportData;
        private System.Windows.Forms.ToolStripMenuItem tsmiImportData;
        private System.Windows.Forms.ToolStripMenuItem tsmiCompareData;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tsmiGenerateColumnDocumentation;
        private System.Windows.Forms.ToolStripMenuItem tsmiDatabaseDiagram;
        private System.Windows.Forms.ToolStripMenuItem tsmiGenerateProcedure;
        private System.Windows.Forms.ToolStripMenuItem tsmiGenerateInsertProcedure;
        private System.Windows.Forms.ToolStripMenuItem tsmiUpdateProcedure;
        private System.Windows.Forms.ToolStripMenuItem tsmiDeleteProcedure;
        private System.Windows.Forms.ToolStripMenuItem tsmiAnalysis;
        private System.Windows.Forms.ToolStripMenuItem tsmiIndexFragmentation;
        private System.Windows.Forms.ToolStripMenuItem tsmiOptimize;
        private System.Windows.Forms.ToolStripMenuItem tsmiNewColumn;
        private System.Windows.Forms.ToolStripMenuItem tsmiModifyColumn;
    }
}
