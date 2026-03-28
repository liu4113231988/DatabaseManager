using AntdUI;
namespace DatabaseManager.Forms
{
    partial class frmBackupSetting
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmBackupSetting));
            this.btnCancel = new AntdUI.Button();
            this.btnSave = new AntdUI.Button();
            this.dgvSettings = new System.Windows.Forms.DataGridView();
            this.colDatabaseType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colClientToolFilePath = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSaveFolder = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colZipBackupFile = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.label1 = new AntdUI.Label();
            this.label2 = new AntdUI.Label();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSettings)).BeginInit();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(398, 382);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(88, 33);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.btnSave.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSave.Location = new System.Drawing.Point(277, 382);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(88, 33);
            this.btnSave.TabIndex = 12;
            this.btnSave.Text = "Save";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // dgvSettings
            // 
            this.dgvSettings.AllowDrop = true;
            this.dgvSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvSettings.Location = new System.Drawing.Point(1, 0);
            this.dgvSettings.Margin = new System.Windows.Forms.Padding(4);
            this.dgvSettings.Name = "dgvSettings";
            this.dgvSettings.Size = new System.Drawing.Size(760, 286);
            this.dgvSettings.TabIndex = 14;
            this.dgvSettings.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvSettings_CellDoubleClick);
            this.dgvSettings.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.dgvSettings_DataBindingComplete);
            // 
            // colDatabaseType
            // 
            this.colDatabaseType.DataPropertyName = "DatabaseType";
            this.colDatabaseType.HeaderText = "Database Type";
            this.colDatabaseType.Name = "colDatabaseType";
            this.colDatabaseType.ReadOnly = true;
            // 
            // colClientToolFilePath
            // 
            this.colClientToolFilePath.DataPropertyName = "ClientToolFilePath";
            this.colClientToolFilePath.HeaderText = "Client Tool File Path";
            this.colClientToolFilePath.Name = "colClientToolFilePath";
            this.colClientToolFilePath.Width = 250;
            // 
            // colSaveFolder
            // 
            this.colSaveFolder.DataPropertyName = "SaveFolder";
            this.colSaveFolder.HeaderText = "Save Folder";
            this.colSaveFolder.Name = "colSaveFolder";
            this.colSaveFolder.Width = 250;
            // 
            // colZipBackupFile
            // 
            this.colZipBackupFile.DataPropertyName = "ZipFile";
            this.colZipBackupFile.HeaderText = "Zip Backup File";
            this.colZipBackupFile.Name = "colZipBackupFile";
            this.colZipBackupFile.Width = 150;
            // 
            // dgvSettings
            // 
            this.dgvSettings.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colDatabaseType,
            this.colClientToolFilePath,
            this.colSaveFolder,
            this.colZipBackupFile});
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Filter = "exe file|*.exe|all files|*.*";
            this.openFileDialog1.RestoreDirectory = true;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.Location = new System.Drawing.Point(69, 296);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(615, 82);
            this.label1.TabIndex = 15;
            this.label1.Text = resources.GetString("label1.Text");
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 296);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(40, 17);
            this.label2.TabIndex = 16;
            this.label2.Text = "Note:";
            // 
            // frmBackupSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(763, 424);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.dgvSettings);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximizeBox = false;
            this.Name = "frmBackupSetting";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Backup Setting";
            this.Load += new System.EventHandler(this.frmBackupSetting_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSettings)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private AntdUI.Button btnCancel;
        private AntdUI.Button btnSave;
        private System.Windows.Forms.DataGridView dgvSettings;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private AntdUI.Label label1;
        private AntdUI.Label label2;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDatabaseType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClientToolFilePath;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSaveFolder;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colZipBackupFile;
    }
}