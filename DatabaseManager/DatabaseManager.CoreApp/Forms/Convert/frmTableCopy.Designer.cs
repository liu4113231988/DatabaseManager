namespace DatabaseManager
{
    partial class frmTableCopy
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmTableCopy));
            rbSameDatabase = new AntdUI.Radio();
            rbAnotherDatabase = new AntdUI.Radio();
            chkScriptData = new AntdUI.Checkbox();
            chkScriptSchema = new AntdUI.Checkbox();
            lblScriptsMode = new AntdUI.Label();
            btnClose = new AntdUI.Button();
            btnExecute = new AntdUI.Button();
            label1 = new AntdUI.Label();
            txtName = new AntdUI.Input();
            ucConnection = new DatabaseManager.Controls.UC_DbConnectionProfile();
            chkGenerateIdentity = new AntdUI.Checkbox();
            lblSchema = new AntdUI.Label();
            cboSchema = new AntdUI.Select();
            chkPrimaryKey = new AntdUI.Checkbox();
            chkForeignKey = new AntdUI.Checkbox();
            chkIndex = new AntdUI.Checkbox();
            chkCheckConstraint = new AntdUI.Checkbox();
            chkTrigger = new AntdUI.Checkbox();
            lblDataTypeMappingFileType = new AntdUI.Label();
            cboDataTypeMappingFile = new AntdUI.Select();
            label2 = new AntdUI.Label();
            chkNeedPreview = new AntdUI.Checkbox();
            picNeedPreviewTip = new System.Windows.Forms.PictureBox();
            toolTip1 = new System.Windows.Forms.ToolTip(components);
            txtMessage = new AntdUI.Input();
            ((System.ComponentModel.ISupportInitialize)picNeedPreviewTip).BeginInit();
            SuspendLayout();
            // 
            // rbSameDatabase
            // 
            rbSameDatabase.AutoSize = true;
            rbSameDatabase.Checked = true;
            rbSameDatabase.Location = new System.Drawing.Point(16, 8);
            rbSameDatabase.Margin = new System.Windows.Forms.Padding(4);
            rbSameDatabase.Name = "rbSameDatabase";
            rbSameDatabase.Size = new System.Drawing.Size(115, 21);
            rbSameDatabase.TabIndex = 1;
            rbSameDatabase.Text = "same database";
            rbSameDatabase.CheckedChanged += (sender, value) => rbSameDatabase_CheckedChanged(sender, new System.EventArgs());
            // 
            // rbAnotherDatabase
            // 
            rbAnotherDatabase.AutoSize = true;
            rbAnotherDatabase.Location = new System.Drawing.Point(144, 8);
            rbAnotherDatabase.Margin = new System.Windows.Forms.Padding(4);
            rbAnotherDatabase.Name = "rbAnotherDatabase";
            rbAnotherDatabase.Size = new System.Drawing.Size(129, 21);
            rbAnotherDatabase.TabIndex = 2;
            rbAnotherDatabase.Text = "another database";
            // 
            // chkScriptData
            // 
            chkScriptData.AutoSize = true;
            chkScriptData.Checked = true;
            chkScriptData.Location = new System.Drawing.Point(176, 180);
            chkScriptData.Margin = new System.Windows.Forms.Padding(4);
            chkScriptData.Name = "chkScriptData";
            chkScriptData.Size = new System.Drawing.Size(53, 21);
            chkScriptData.TabIndex = 15;
            chkScriptData.Text = "data";
            chkScriptData.CheckedChanged += (sender, value) => chkScriptData_CheckedChanged(sender, new System.EventArgs());
            // 
            // chkScriptSchema
            // 
            chkScriptSchema.AutoSize = true;
            chkScriptSchema.Checked = true;
            chkScriptSchema.Location = new System.Drawing.Point(83, 180);
            chkScriptSchema.Margin = new System.Windows.Forms.Padding(4);
            chkScriptSchema.Name = "chkScriptSchema";
            chkScriptSchema.Size = new System.Drawing.Size(71, 21);
            chkScriptSchema.TabIndex = 14;
            chkScriptSchema.Text = "schema";
            chkScriptSchema.CheckedChanged += (sender, value) => chkScriptSchema_CheckedChanged(sender, new System.EventArgs());
            // 
            // lblScriptsMode
            // 
            lblScriptsMode.AutoSize = true;
            lblScriptsMode.Location = new System.Drawing.Point(15, 181);
            lblScriptsMode.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblScriptsMode.Name = "lblScriptsMode";
            lblScriptsMode.Size = new System.Drawing.Size(46, 17);
            lblScriptsMode.TabIndex = 13;
            lblScriptsMode.Text = "Mode:";
            // 
            // btnClose
            // 
            btnClose.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnClose.Location = new System.Drawing.Point(352, 320);
            btnClose.Margin = new System.Windows.Forms.Padding(4);
            btnClose.Name = "btnClose";
            btnClose.Size = new System.Drawing.Size(88, 36);
            btnClose.TabIndex = 24;
            btnClose.Text = "Close";
            btnClose.Click += btnCancel_Click;
            // 
            // btnExecute
            // 
            btnExecute.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnExecute.Location = new System.Drawing.Point(239, 320);
            btnExecute.Margin = new System.Windows.Forms.Padding(4);
            btnExecute.Name = "btnExecute";
            btnExecute.Size = new System.Drawing.Size(88, 36);
            btnExecute.TabIndex = 23;
            btnExecute.Text = "Execute";
            btnExecute.Click += btnExecute_Click;
            // 
            // txtName
            // 
            txtName.Location = new System.Drawing.Point(83, 89);
            txtName.Margin = new System.Windows.Forms.Padding(4);
            txtName.Name = "txtName";
            txtName.Size = new System.Drawing.Size(302, 32);
            txtName.TabIndex = 26;
            // 
            // ucConnection
            // 
            ucConnection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ucConnection.DatabaseType = DatabaseInterpreter.Model.DatabaseType.Unknown;
            ucConnection.Enabled = false;
            ucConnection.EnableDatabaseType = true;
            ucConnection.Location = new System.Drawing.Point(12, 42);
            ucConnection.Margin = new System.Windows.Forms.Padding(0);
            ucConnection.Name = "ucConnection";
            ucConnection.Size = new System.Drawing.Size(666, 33);
            ucConnection.TabIndex = 0;
            ucConnection.Title = "Target:";
            ucConnection.ProfileSelectedChanged += ucConnection_ProfileSelectedChanged;
            ucConnection.DatabaseTypeSelectedChanged += ucConnection_DatabaseTypeSelectedChanged;
            // 
            // chkGenerateIdentity
            // 
            chkGenerateIdentity.AutoSize = true;
            chkGenerateIdentity.Checked = true;
            chkGenerateIdentity.CheckState = System.Windows.Forms.CheckState.Checked;
            chkGenerateIdentity.Location = new System.Drawing.Point(19, 240);
            chkGenerateIdentity.Margin = new System.Windows.Forms.Padding(4);
            chkGenerateIdentity.Name = "chkGenerateIdentity";
            chkGenerateIdentity.Size = new System.Drawing.Size(126, 21);
            chkGenerateIdentity.TabIndex = 27;
            chkGenerateIdentity.Text = "Generate identity";
            // 
            // lblSchema
            // 
            lblSchema.AutoSize = true;
            lblSchema.Location = new System.Drawing.Point(449, 92);
            lblSchema.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblSchema.Name = "lblSchema";
            lblSchema.Size = new System.Drawing.Size(56, 17);
            lblSchema.TabIndex = 29;
            lblSchema.Text = "Schema:";
            // 
            // cboSchema
            // 
            cboSchema.Location = new System.Drawing.Point(517, 87);
            cboSchema.Name = "cboSchema";
            cboSchema.Size = new System.Drawing.Size(121, 32);
            cboSchema.TabIndex = 30;
            // 
            // chkPrimaryKey
            // 
            chkPrimaryKey.AutoSize = true;
            chkPrimaryKey.Location = new System.Drawing.Point(19, 211);
            chkPrimaryKey.Margin = new System.Windows.Forms.Padding(4);
            chkPrimaryKey.Name = "chkPrimaryKey";
            chkPrimaryKey.Size = new System.Drawing.Size(96, 21);
            chkPrimaryKey.TabIndex = 31;
            chkPrimaryKey.Text = "Primary Key";
            // 
            // chkForeignKey
            // 
            chkForeignKey.AutoSize = true;
            chkForeignKey.Location = new System.Drawing.Point(143, 211);
            chkForeignKey.Margin = new System.Windows.Forms.Padding(4);
            chkForeignKey.Name = "chkForeignKey";
            chkForeignKey.Size = new System.Drawing.Size(96, 21);
            chkForeignKey.TabIndex = 32;
            chkForeignKey.Text = "Foreign Key";
            // 
            // chkIndex
            // 
            chkIndex.AutoSize = true;
            chkIndex.Location = new System.Drawing.Point(267, 211);
            chkIndex.Margin = new System.Windows.Forms.Padding(4);
            chkIndex.Name = "chkIndex";
            chkIndex.Size = new System.Drawing.Size(59, 21);
            chkIndex.TabIndex = 33;
            chkIndex.Text = "Index";
            // 
            // chkCheckConstraint
            // 
            chkCheckConstraint.AutoSize = true;
            chkCheckConstraint.Location = new System.Drawing.Point(352, 211);
            chkCheckConstraint.Margin = new System.Windows.Forms.Padding(4);
            chkCheckConstraint.Name = "chkCheckConstraint";
            chkCheckConstraint.Size = new System.Drawing.Size(125, 21);
            chkCheckConstraint.TabIndex = 34;
            chkCheckConstraint.Text = "Check Constraint";
            // 
            // chkGenerateIdentity
            // 
            chkGenerateIdentity.AutoSize = true;
            chkGenerateIdentity.Checked = true;
            chkGenerateIdentity.Location = new System.Drawing.Point(19, 240);
            chkGenerateIdentity.Margin = new System.Windows.Forms.Padding(4);
            chkGenerateIdentity.Name = "chkGenerateIdentity";
            chkGenerateIdentity.Size = new System.Drawing.Size(126, 21);
            chkGenerateIdentity.TabIndex = 27;
            chkGenerateIdentity.Text = "Generate identity";
            // 
            // cboSchema
            // 
            cboSchema.Location = new System.Drawing.Point(517, 87);
            cboSchema.Name = "cboSchema";
            cboSchema.Size = new System.Drawing.Size(121, 32);
            cboSchema.TabIndex = 30;
            // 
            // chkPrimaryKey
            // 
            chkPrimaryKey.AutoSize = true;
            chkPrimaryKey.Location = new System.Drawing.Point(19, 211);
            chkPrimaryKey.Margin = new System.Windows.Forms.Padding(4);
            chkPrimaryKey.Name = "chkPrimaryKey";
            chkPrimaryKey.Size = new System.Drawing.Size(96, 21);
            chkPrimaryKey.TabIndex = 31;
            chkPrimaryKey.Text = "Primary Key";
            // 
            // chkForeignKey
            // 
            chkForeignKey.AutoSize = true;
            chkForeignKey.Location = new System.Drawing.Point(143, 211);
            chkForeignKey.Margin = new System.Windows.Forms.Padding(4);
            chkForeignKey.Name = "chkForeignKey";
            chkForeignKey.Size = new System.Drawing.Size(96, 21);
            chkForeignKey.TabIndex = 32;
            chkForeignKey.Text = "Foreign Key";
            // 
            // chkIndex
            // 
            chkIndex.AutoSize = true;
            chkIndex.Location = new System.Drawing.Point(267, 211);
            chkIndex.Margin = new System.Windows.Forms.Padding(4);
            chkIndex.Name = "chkIndex";
            chkIndex.Size = new System.Drawing.Size(59, 21);
            chkIndex.TabIndex = 33;
            chkIndex.Text = "Index";
            // 
            // chkCheckConstraint
            // 
            chkCheckConstraint.AutoSize = true;
            chkCheckConstraint.Location = new System.Drawing.Point(352, 211);
            chkCheckConstraint.Margin = new System.Windows.Forms.Padding(4);
            chkCheckConstraint.Name = "chkCheckConstraint";
            chkCheckConstraint.Size = new System.Drawing.Size(125, 21);
            chkCheckConstraint.TabIndex = 34;
            chkCheckConstraint.Text = "Check Constraint";
            // 
            // chkTrigger
            // 
            chkTrigger.AutoSize = true;
            chkTrigger.Location = new System.Drawing.Point(512, 211);
            chkTrigger.Margin = new System.Windows.Forms.Padding(4);
            chkTrigger.Name = "chkTrigger";
            chkTrigger.Size = new System.Drawing.Size(70, 21);
            chkTrigger.TabIndex = 35;
            chkTrigger.Text = "Trigger";
            // 
            // lblDataTypeMappingFileType
            // 
            lblDataTypeMappingFileType.AutoSize = true;
            lblDataTypeMappingFileType.Location = new System.Drawing.Point(331, 142);
            lblDataTypeMappingFileType.Name = "lblDataTypeMappingFileType";
            lblDataTypeMappingFileType.Size = new System.Drawing.Size(0, 17);
            lblDataTypeMappingFileType.TabIndex = 71;
            lblDataTypeMappingFileType.Click += lblDataTypeMappingFileType_Click;
            // 
            // cboDataTypeMappingFile
            // 
            cboDataTypeMappingFile.Location = new System.Drawing.Point(163, 137);
            cboDataTypeMappingFile.Name = "cboDataTypeMappingFile";
            cboDataTypeMappingFile.Size = new System.Drawing.Size(162, 32);
            cboDataTypeMappingFile.TabIndex = 70;
            cboDataTypeMappingFile.SelectedIndexChanged += (sender, index) => cboDataTypeMappingFile_SelectedIndexChanged(sender, new System.EventArgs());
            // 
            // chkNeedPreview
            // 
            chkNeedPreview.AutoSize = true;
            chkNeedPreview.Enabled = false;
            chkNeedPreview.Location = new System.Drawing.Point(19, 269);
            chkNeedPreview.Margin = new System.Windows.Forms.Padding(4);
            chkNeedPreview.Name = "chkNeedPreview";
            chkNeedPreview.Size = new System.Drawing.Size(108, 21);
            chkNeedPreview.TabIndex = 72;
            chkNeedPreview.Tag = "Schema";
            chkNeedPreview.Text = "Need preview";
            // 
            // picNeedPreviewTip
            // 
            picNeedPreviewTip.Location = new System.Drawing.Point(133, 269);
            picNeedPreviewTip.Name = "picNeedPreviewTip";
            picNeedPreviewTip.Size = new System.Drawing.Size(17, 19);
            picNeedPreviewTip.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            picNeedPreviewTip.TabIndex = 73;
            picNeedPreviewTip.TabStop = false;
            toolTip1.SetToolTip(picNeedPreviewTip, "You can edit the target data type in the previewer.");
            // 
            // txtMessage
            // 
            txtMessage.BackColor = System.Drawing.Color.White;
            txtMessage.Dock = System.Windows.Forms.DockStyle.Bottom;
            txtMessage.Location = new System.Drawing.Point(0, 370);
            txtMessage.Multiline = true;
            txtMessage.Name = "txtMessage";
            txtMessage.ReadOnly = true;
            txtMessage.Size = new System.Drawing.Size(686, 84);
            txtMessage.TabIndex = 74;
            // 
            // frmTableCopy
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(686, 454);
            Controls.Add(txtMessage);
            Controls.Add(picNeedPreviewTip);
            Controls.Add(chkNeedPreview);
            Controls.Add(lblDataTypeMappingFileType);
            Controls.Add(cboDataTypeMappingFile);
            Controls.Add(label2);
            Controls.Add(chkTrigger);
            Controls.Add(chkCheckConstraint);
            Controls.Add(chkIndex);
            Controls.Add(chkForeignKey);
            Controls.Add(chkPrimaryKey);
            Controls.Add(cboSchema);
            Controls.Add(lblSchema);
            Controls.Add(chkGenerateIdentity);
            Controls.Add(txtName);
            Controls.Add(label1);
            Controls.Add(btnClose);
            Controls.Add(btnExecute);
            Controls.Add(chkScriptData);
            Controls.Add(chkScriptSchema);
            Controls.Add(lblScriptsMode);
            Controls.Add(rbAnotherDatabase);
            Controls.Add(rbSameDatabase);
            Controls.Add(ucConnection);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            Name = "frmTableCopy";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Copy Table";
            Load += frmDbObjectCopy_Load;
            ((System.ComponentModel.ISupportInitialize)picNeedPreviewTip).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Controls.UC_DbConnectionProfile ucConnection;
        private AntdUI.Radio rbSameDatabase;
        private AntdUI.Radio rbAnotherDatabase;
        private AntdUI.Checkbox chkScriptData;
        private AntdUI.Checkbox chkScriptSchema;
        private AntdUI.Label lblScriptsMode;
        private AntdUI.Button btnClose;
        private AntdUI.Button btnExecute;
        private AntdUI.Label label1;
        private AntdUI.Input txtName;
        private AntdUI.Checkbox chkGenerateIdentity;
        private AntdUI.Label lblSchema;
        private AntdUI.Select cboSchema;
        private AntdUI.Checkbox chkPrimaryKey;
        private AntdUI.Checkbox chkForeignKey;
        private AntdUI.Checkbox chkIndex;
        private AntdUI.Checkbox chkCheckConstraint;
        private AntdUI.Checkbox chkTrigger;
        private AntdUI.Label lblDataTypeMappingFileType;
        private AntdUI.Select cboDataTypeMappingFile;
        private AntdUI.Label label2;
        private AntdUI.Checkbox chkNeedPreview;
        private System.Windows.Forms.PictureBox picNeedPreviewTip;
        private System.Windows.Forms.ToolTip toolTip1;
        private AntdUI.Input txtMessage;
    }
}