using AntdUI;
using WeifenLuo.WinFormsUI.Docking;
namespace DatabaseManager.Forms
{
    partial class frmMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            menuStrip1 = new System.Windows.Forms.MenuStrip();
            tsmiSettings = new System.Windows.Forms.ToolStripMenuItem();
            tsmiSetting = new System.Windows.Forms.ToolStripMenuItem();
            tsmiDbConnection = new System.Windows.Forms.ToolStripMenuItem();
            tsmiBackupSetting = new System.Windows.Forms.ToolStripMenuItem();
            tsmiLock = new System.Windows.Forms.ToolStripMenuItem();
            tsmiViews = new System.Windows.Forms.ToolStripMenuItem();
            tsmiObjectsExplorer = new System.Windows.Forms.ToolStripMenuItem();
            tsmiMessage = new System.Windows.Forms.ToolStripMenuItem();
            tsmiTools = new System.Windows.Forms.ToolStripMenuItem();
            tsmiWktView = new System.Windows.Forms.ToolStripMenuItem();
            tsmiImageViewer = new System.Windows.Forms.ToolStripMenuItem();
            tsmiJsonViwer = new System.Windows.Forms.ToolStripMenuItem();
            tsmiCodeGenerator = new System.Windows.Forms.ToolStripMenuItem();
            panelToolbar = new Panel();
            flowLayoutPanelToolbar = new System.Windows.Forms.FlowLayoutPanel();
            btnNewQuery = new Button();
            btnOpen = new Button();
            btnSave = new Button();
            separator1 = new System.Windows.Forms.Label();
            btnGenerateScripts = new Button();
            btnCompare = new Button();
            btnDataCompare = new Button();
            separator2 = new System.Windows.Forms.Label();
            btnConvert = new Button();
            btnTranslateScript = new Button();
            separator3 = new System.Windows.Forms.Label();
            btnRun = new Button();
            dlgOpenFile = new System.Windows.Forms.OpenFileDialog();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            dockPanelMain = new DockPanel();
            menuStrip1.SuspendLayout();
            panelToolbar.SuspendLayout();
            flowLayoutPanelToolbar.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.BackColor = System.Drawing.Color.White;
            menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiSettings, tsmiViews, tsmiTools });
            menuStrip1.Location = new System.Drawing.Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new System.Windows.Forms.Padding(7, 3, 0, 3);
            menuStrip1.Size = new System.Drawing.Size(917, 27);
            menuStrip1.TabIndex = 5;
            menuStrip1.Text = "menuStrip1";
            // 
            // tsmiSettings
            // 
            tsmiSettings.BackColor = System.Drawing.Color.White;
            tsmiSettings.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            tsmiSettings.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiSetting, tsmiDbConnection, tsmiBackupSetting, tsmiLock });
            tsmiSettings.Name = "tsmiSettings";
            tsmiSettings.Size = new System.Drawing.Size(66, 21);
            tsmiSettings.Text = "Settings";
            // 
            // tsmiSetting
            // 
            tsmiSetting.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiSetting.Name = "tsmiSetting";
            tsmiSetting.Size = new System.Drawing.Size(163, 22);
            tsmiSetting.Text = "Setting";
            tsmiSetting.Click += tsmiSetting_Click;
            // 
            // tsmiDbConnection
            // 
            tsmiDbConnection.Image = Resources.DbConnect16;
            tsmiDbConnection.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiDbConnection.Name = "tsmiDbConnection";
            tsmiDbConnection.Size = new System.Drawing.Size(163, 22);
            tsmiDbConnection.Text = "Connection";
            tsmiDbConnection.Click += tsmiDbConnection_Click;
            // 
            // tsmiBackupSetting
            // 
            tsmiBackupSetting.Image = Resources.DbBackup;
            tsmiBackupSetting.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiBackupSetting.Name = "tsmiBackupSetting";
            tsmiBackupSetting.Size = new System.Drawing.Size(163, 22);
            tsmiBackupSetting.Text = "Backup Setting";
            tsmiBackupSetting.Click += tsmiBackupSetting_Click;
            // 
            // tsmiLock
            // 
            tsmiLock.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiLock.Name = "tsmiLock";
            tsmiLock.Size = new System.Drawing.Size(163, 22);
            tsmiLock.Text = "Lock";
            tsmiLock.Click += tsmiLock_Click;
            // 
            // tsmiViews
            // 
            tsmiViews.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiObjectsExplorer, tsmiMessage });
            tsmiViews.Name = "tsmiViews";
            tsmiViews.Size = new System.Drawing.Size(53, 21);
            tsmiViews.Text = "Views";
            // 
            // tsmiObjectsExplorer
            // 
            tsmiObjectsExplorer.Name = "tsmiObjectsExplorer";
            tsmiObjectsExplorer.Size = new System.Drawing.Size(173, 22);
            tsmiObjectsExplorer.Text = "Objects Explorer";
            tsmiObjectsExplorer.Click += tsmiObjectsExplorer_Click;
            // 
            // tsmiMessage
            // 
            tsmiMessage.Name = "tsmiMessage";
            tsmiMessage.Size = new System.Drawing.Size(173, 22);
            tsmiMessage.Text = "Message";
            tsmiMessage.Click += tsmiMessage_Click;
            // 
            // tsmiTools
            // 
            tsmiTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { tsmiWktView, tsmiImageViewer, tsmiJsonViwer, tsmiCodeGenerator });
            tsmiTools.Name = "tsmiTools";
            tsmiTools.Size = new System.Drawing.Size(52, 21);
            tsmiTools.Text = "Tools";
            // 
            // tsmiWktView
            // 
            tsmiWktView.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiWktView.Name = "tsmiWktView";
            tsmiWktView.Size = new System.Drawing.Size(170, 22);
            tsmiWktView.Text = "WKT Viewer";
            tsmiWktView.Click += tsmiWktView_Click;
            // 
            // tsmiImageViewer
            // 
            tsmiImageViewer.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiImageViewer.Name = "tsmiImageViewer";
            tsmiImageViewer.Size = new System.Drawing.Size(170, 22);
            tsmiImageViewer.Text = "Image Viewer";
            tsmiImageViewer.Click += tsmiImageViewer_Click;
            // 
            // tsmiJsonViwer
            // 
            tsmiJsonViwer.Image = Resources.JSON;
            tsmiJsonViwer.Name = "tsmiJsonViwer";
            tsmiJsonViwer.Size = new System.Drawing.Size(170, 22);
            tsmiJsonViwer.Text = "JSON Viewer";
            tsmiJsonViwer.Click += tsmiJsonViwer_Click;
            // 
            // tsmiCodeGenerator
            // 
            tsmiCodeGenerator.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            tsmiCodeGenerator.Name = "tsmiCodeGenerator";
            tsmiCodeGenerator.Size = new System.Drawing.Size(170, 22);
            tsmiCodeGenerator.Text = "Code Generator";
            tsmiCodeGenerator.Click += tsmiCodeGenerator_Click;
            // 
            // panelToolbar
            // 
            panelToolbar.Controls.Add(flowLayoutPanelToolbar);
            panelToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            panelToolbar.Location = new System.Drawing.Point(0, 27);
            panelToolbar.Name = "panelToolbar";
            panelToolbar.Size = new System.Drawing.Size(917, 64);
            panelToolbar.TabIndex = 10;
            // 
            // flowLayoutPanelToolbar
            // 
            flowLayoutPanelToolbar.Controls.Add(btnNewQuery);
            flowLayoutPanelToolbar.Controls.Add(btnOpen);
            flowLayoutPanelToolbar.Controls.Add(btnSave);
            flowLayoutPanelToolbar.Controls.Add(separator1);
            flowLayoutPanelToolbar.Controls.Add(btnGenerateScripts);
            flowLayoutPanelToolbar.Controls.Add(btnCompare);
            flowLayoutPanelToolbar.Controls.Add(btnDataCompare);
            flowLayoutPanelToolbar.Controls.Add(separator2);
            flowLayoutPanelToolbar.Controls.Add(btnConvert);
            flowLayoutPanelToolbar.Controls.Add(btnTranslateScript);
            flowLayoutPanelToolbar.Controls.Add(separator3);
            flowLayoutPanelToolbar.Controls.Add(btnRun);
            flowLayoutPanelToolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            flowLayoutPanelToolbar.Location = new System.Drawing.Point(0, 0);
            flowLayoutPanelToolbar.Name = "flowLayoutPanelToolbar";
            flowLayoutPanelToolbar.Size = new System.Drawing.Size(917, 64);
            flowLayoutPanelToolbar.TabIndex = 0;
            // 
            // btnNewQuery
            // 
            btnNewQuery.Icon = (System.Drawing.Image)resources.GetObject("btnNewQuery.Icon");
            btnNewQuery.IconRatio = 1F;
            btnNewQuery.Location = new System.Drawing.Point(3, 3);
            btnNewQuery.Name = "btnNewQuery";
            btnNewQuery.Size = new System.Drawing.Size(36, 36);
            btnNewQuery.TabIndex = 0;
            toolTip1.SetToolTip(btnNewQuery, "New Query");
            btnNewQuery.Click += tsBtnAddQuery_Click;
            // 
            // btnOpen
            // 
            btnOpen.Icon = (System.Drawing.Image)resources.GetObject("btnOpen.Icon");
            btnOpen.IconRatio = 1F;
            btnOpen.Location = new System.Drawing.Point(44, 2);
            btnOpen.Margin = new System.Windows.Forms.Padding(2);
            btnOpen.Name = "btnOpen";
            btnOpen.Size = new System.Drawing.Size(36, 36);
            btnOpen.TabIndex = 1;
            toolTip1.SetToolTip(btnOpen, "Open File");
            btnOpen.Click += tsBtnOpenFile_Click;
            // 
            // btnSave
            // 
            btnSave.Icon = (System.Drawing.Image)resources.GetObject("btnSave.Icon");
            btnSave.IconRatio = 1F;
            btnSave.Location = new System.Drawing.Point(84, 2);
            btnSave.Margin = new System.Windows.Forms.Padding(2);
            btnSave.Name = "btnSave";
            btnSave.Size = new System.Drawing.Size(36, 36);
            btnSave.TabIndex = 2;
            toolTip1.SetToolTip(btnSave, "Save (Ctrl+S)");
            btnSave.Click += tsBtnSave_Click;
            // 
            // separator1
            // 
            separator1.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            separator1.Location = new System.Drawing.Point(125, 0);
            separator1.Name = "separator1";
            separator1.Size = new System.Drawing.Size(1, 40);
            separator1.TabIndex = 3;
            // 
            // btnGenerateScripts
            // 
            btnGenerateScripts.Icon = (System.Drawing.Image)resources.GetObject("btnGenerateScripts.Icon");
            btnGenerateScripts.IconRatio = 1F;
            btnGenerateScripts.Location = new System.Drawing.Point(131, 2);
            btnGenerateScripts.Margin = new System.Windows.Forms.Padding(2);
            btnGenerateScripts.Name = "btnGenerateScripts";
            btnGenerateScripts.Size = new System.Drawing.Size(36, 36);
            btnGenerateScripts.TabIndex = 4;
            toolTip1.SetToolTip(btnGenerateScripts, "Generate Scripts");
            btnGenerateScripts.Click += tsBtnGenerateScripts_Click;
            // 
            // btnCompare
            // 
            btnCompare.Icon = (System.Drawing.Image)resources.GetObject("btnCompare.Icon");
            btnCompare.IconRatio = 1F;
            btnCompare.Location = new System.Drawing.Point(171, 2);
            btnCompare.Margin = new System.Windows.Forms.Padding(2);
            btnCompare.Name = "btnCompare";
            btnCompare.Size = new System.Drawing.Size(36, 36);
            btnCompare.TabIndex = 5;
            toolTip1.SetToolTip(btnCompare, "Schema Compare");
            btnCompare.Click += tsBtnCompare_Click;
            // 
            // btnDataCompare
            // 
            btnDataCompare.Icon = Resources.DataCompare32;
            btnDataCompare.IconRatio = 1F;
            btnDataCompare.Location = new System.Drawing.Point(211, 2);
            btnDataCompare.Margin = new System.Windows.Forms.Padding(2);
            btnDataCompare.Name = "btnDataCompare";
            btnDataCompare.Size = new System.Drawing.Size(36, 36);
            btnDataCompare.TabIndex = 6;
            toolTip1.SetToolTip(btnDataCompare, "Data Compare");
            btnDataCompare.Click += tsBtnDataCompare_Click;
            // 
            // separator2
            // 
            separator2.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            separator2.Location = new System.Drawing.Point(252, 0);
            separator2.Name = "separator2";
            separator2.Size = new System.Drawing.Size(1, 40);
            separator2.TabIndex = 7;
            // 
            // btnConvert
            // 
            btnConvert.Icon = (System.Drawing.Image)resources.GetObject("btnConvert.Icon");
            btnConvert.IconRatio = 1F;
            btnConvert.Location = new System.Drawing.Point(258, 2);
            btnConvert.Margin = new System.Windows.Forms.Padding(2);
            btnConvert.Name = "btnConvert";
            btnConvert.Size = new System.Drawing.Size(36, 36);
            btnConvert.TabIndex = 8;
            toolTip1.SetToolTip(btnConvert, "Convert Database");
            btnConvert.Click += tsBtnConvert_Click;
            // 
            // btnTranslateScript
            // 
            btnTranslateScript.Icon = Resources.Translate;
            btnTranslateScript.IconRatio = 1F;
            btnTranslateScript.Location = new System.Drawing.Point(298, 2);
            btnTranslateScript.Margin = new System.Windows.Forms.Padding(2);
            btnTranslateScript.Name = "btnTranslateScript";
            btnTranslateScript.Size = new System.Drawing.Size(36, 36);
            btnTranslateScript.TabIndex = 9;
            toolTip1.SetToolTip(btnTranslateScript, "Translate Script");
            btnTranslateScript.Click += tsBtnTranslateScript_Click;
            // 
            // separator3
            // 
            separator3.BackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            separator3.Location = new System.Drawing.Point(339, 0);
            separator3.Name = "separator3";
            separator3.Size = new System.Drawing.Size(1, 40);
            separator3.TabIndex = 10;
            // 
            // btnRun
            // 
            btnRun.Icon = (System.Drawing.Image)resources.GetObject("btnRun.Icon");
            btnRun.IconRatio = 1F;
            btnRun.Location = new System.Drawing.Point(345, 2);
            btnRun.Margin = new System.Windows.Forms.Padding(2);
            btnRun.Name = "btnRun";
            btnRun.Size = new System.Drawing.Size(36, 36);
            btnRun.TabIndex = 11;
            toolTip1.SetToolTip(btnRun, "Run (F5)");
            btnRun.Click += tsBtnRun_Click;
            // 
            // dlgOpenFile
            // 
            dlgOpenFile.Filter = "sql file|*.sql|all files|*.*";
            // 
            // dockPanelMain
            // 
            dockPanelMain.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dockPanelMain.Location = new System.Drawing.Point(0, 70);
            dockPanelMain.Name = "dockPanelMain";
            dockPanelMain.Size = new System.Drawing.Size(917, 506);
            dockPanelMain.TabIndex = 12;
            // 
            // frmMain
            // 
            AllowDrop = true;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.Control;
            ClientSize = new System.Drawing.Size(917, 579);
            Controls.Add(dockPanelMain);
            Controls.Add(panelToolbar);
            Controls.Add(menuStrip1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Margin = new System.Windows.Forms.Padding(4);
            Name = "frmMain";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Database Manager";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            FormClosing += frmMain_FormClosing;
            Load += frmMain_Load;
            DragDrop += frmMain_DragDrop;
            DragOver += frmMain_DragOver;
            KeyDown += frmMain_KeyDown;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            panelToolbar.ResumeLayout(false);
            flowLayoutPanelToolbar.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem tsmiSettings;
        private System.Windows.Forms.ToolStripMenuItem tsmiSetting;
        private System.Windows.Forms.ToolStripMenuItem tsmiDbConnection;
        private System.Windows.Forms.ToolStripMenuItem tsmiBackupSetting;
        private System.Windows.Forms.ToolStripMenuItem tsmiLock;
        private System.Windows.Forms.ToolStripMenuItem tsmiViews;
        private System.Windows.Forms.ToolStripMenuItem tsmiObjectsExplorer;
        private System.Windows.Forms.ToolStripMenuItem tsmiMessage;
        private System.Windows.Forms.ToolStripMenuItem tsmiTools;
        private System.Windows.Forms.ToolStripMenuItem tsmiWktView;
        private System.Windows.Forms.ToolStripMenuItem tsmiImageViewer;
        private System.Windows.Forms.ToolStripMenuItem tsmiJsonViwer;
        private System.Windows.Forms.ToolStripMenuItem tsmiCodeGenerator;
        private AntdUI.Panel panelToolbar;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelToolbar;
        private AntdUI.Button btnNewQuery;
        private AntdUI.Button btnOpen;
        private AntdUI.Button btnSave;
        private System.Windows.Forms.Label separator1;
        private AntdUI.Button btnGenerateScripts;
        private AntdUI.Button btnCompare;
        private AntdUI.Button btnDataCompare;
        private System.Windows.Forms.Label separator2;
        private AntdUI.Button btnConvert;
        private AntdUI.Button btnTranslateScript;
        private System.Windows.Forms.Label separator3;
        private AntdUI.Button btnRun;
        private System.Windows.Forms.OpenFileDialog dlgOpenFile;
        private System.Windows.Forms.ToolTip toolTip1;
        private WeifenLuo.WinFormsUI.Docking.DockPanel dockPanelMain;
    }
}