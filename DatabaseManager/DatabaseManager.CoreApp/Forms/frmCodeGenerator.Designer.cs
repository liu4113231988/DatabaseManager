namespace DatabaseManager
{
    partial class frmCodeGenerator
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmCodeGenerator));
            dlgOutputFolder = new System.Windows.Forms.FolderBrowserDialog();
            splitContainer1 = new System.Windows.Forms.SplitContainer();
            chkGenerateComments = new AntdUI.Checkbox();
            txtNamespance = new AntdUI.Input();
            lblNamespace = new AntdUI.Label();
            label1 = new AntdUI.Label();
            cboLanguage = new AntdUI.Select();
            lblOutputFolder = new AntdUI.Label();
            btnOutputFolder = new AntdUI.Button();
            txtOutputFolder = new AntdUI.Input();
            btnCancel = new AntdUI.Button();
            btnGenerate = new AntdUI.Button();
            tvDbObjects = new DatabaseManager.Controls.UC_DbObjectsSimpleTree();
            btnConnect = new AntdUI.Button();
            dbConnectionProfile = new DatabaseManager.Controls.UC_DbConnectionProfile();
            txtMessage = new AntdUI.Input();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            splitContainer1.Location = new System.Drawing.Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(chkGenerateComments);
            splitContainer1.Panel1.Controls.Add(txtNamespance);
            splitContainer1.Panel1.Controls.Add(lblNamespace);
            splitContainer1.Panel1.Controls.Add(label1);
            splitContainer1.Panel1.Controls.Add(cboLanguage);
            splitContainer1.Panel1.Controls.Add(lblOutputFolder);
            splitContainer1.Panel1.Controls.Add(btnOutputFolder);
            splitContainer1.Panel1.Controls.Add(txtOutputFolder);
            splitContainer1.Panel1.Controls.Add(btnCancel);
            splitContainer1.Panel1.Controls.Add(btnGenerate);
            splitContainer1.Panel1.Controls.Add(tvDbObjects);
            splitContainer1.Panel1.Controls.Add(btnConnect);
            splitContainer1.Panel1.Controls.Add(dbConnectionProfile);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtMessage);
            splitContainer1.Panel2MinSize = 40;
            splitContainer1.Size = new System.Drawing.Size(752, 537);
            splitContainer1.SplitterDistance = 460;
            splitContainer1.TabIndex = 64;
            // 
            // chkGenerateComments
            // 
            chkGenerateComments.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            chkGenerateComments.AutoSize = true;
            chkGenerateComments.Checked = true;
            chkGenerateComments.Location = new System.Drawing.Point(11, 340);
            chkGenerateComments.Name = "chkGenerateComments";
            chkGenerateComments.Size = new System.Drawing.Size(185, 21);
            chkGenerateComments.TabIndex = 76;
            chkGenerateComments.Text = "Generate comments if exist";
            // 
            // txtNamespance
            // 
            txtNamespance.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            txtNamespance.Location = new System.Drawing.Point(428, 303);
            txtNamespance.Name = "txtNamespance";
            txtNamespance.Size = new System.Drawing.Size(259, 32);
            txtNamespance.TabIndex = 75;
            // 
            // lblNamespace
            // 
            lblNamespace.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            lblNamespace.Location = new System.Drawing.Point(341, 306);
            lblNamespace.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblNamespace.Name = "lblNamespace";
            lblNamespace.Size = new System.Drawing.Size(80, 17);
            lblNamespace.TabIndex = 74;
            lblNamespace.Text = "Namespace:";
            lblNamespace.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label1
            // 
            label1.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(8, 306);
            label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(68, 17);
            label1.TabIndex = 73;
            label1.Text = "Language:";
            // 
            // cboLanguage
            // 
            cboLanguage.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            cboLanguage.Location = new System.Drawing.Point(83, 303);
            cboLanguage.Name = "cboLanguage";
            cboLanguage.Size = new System.Drawing.Size(121, 32);
            cboLanguage.TabIndex = 72;
            cboLanguage.SelectedIndexChanged += (sender, index) => cboLanguage_SelectedIndexChanged(sender, new System.EventArgs());
            // 
            // lblOutputFolder
            // 
            lblOutputFolder.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            lblOutputFolder.AutoSize = true;
            lblOutputFolder.Location = new System.Drawing.Point(8, 375);
            lblOutputFolder.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblOutputFolder.Name = "lblOutputFolder";
            lblOutputFolder.Size = new System.Drawing.Size(92, 17);
            lblOutputFolder.TabIndex = 71;
            lblOutputFolder.Text = "Output Folder:";
            // 
            // btnOutputFolder
            // 
            btnOutputFolder.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnOutputFolder.Location = new System.Drawing.Point(695, 370);
            btnOutputFolder.Margin = new System.Windows.Forms.Padding(4);
            btnOutputFolder.Name = "btnOutputFolder";
            btnOutputFolder.Size = new System.Drawing.Size(42, 32);
            btnOutputFolder.TabIndex = 70;
            btnOutputFolder.Text = "...";
            btnOutputFolder.Click += btnOutputFolder_Click;
            // 
            // txtOutputFolder
            // 
            txtOutputFolder.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            txtOutputFolder.Location = new System.Drawing.Point(118, 371);
            txtOutputFolder.Margin = new System.Windows.Forms.Padding(4);
            txtOutputFolder.Name = "txtOutputFolder";
            txtOutputFolder.Size = new System.Drawing.Size(571, 32);
            txtOutputFolder.TabIndex = 69;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnCancel.Enabled = false;
            btnCancel.Location = new System.Drawing.Point(400, 407);
            btnCancel.Margin = new System.Windows.Forms.Padding(4);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(88, 36);
            btnCancel.TabIndex = 68;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // btnGenerate
            // 
            btnGenerate.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnGenerate.Location = new System.Drawing.Point(287, 407);
            btnGenerate.Margin = new System.Windows.Forms.Padding(4);
            btnGenerate.Name = "btnGenerate";
            btnGenerate.Size = new System.Drawing.Size(88, 36);
            btnGenerate.TabIndex = 67;
            btnGenerate.Text = "Generate";
            btnGenerate.Click += btnGenerate_Click;
            // 
            // tvDbObjects
            // 
            tvDbObjects.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            tvDbObjects.Location = new System.Drawing.Point(2, 37);
            tvDbObjects.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            tvDbObjects.Name = "tvDbObjects";
            tvDbObjects.ShowCheckBox = true;
            tvDbObjects.Size = new System.Drawing.Size(749, 256);
            tvDbObjects.TabIndex = 66;
            // 
            // btnConnect
            // 
            btnConnect.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnConnect.Location = new System.Drawing.Point(664, 6);
            btnConnect.Margin = new System.Windows.Forms.Padding(4);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new System.Drawing.Size(82, 32);
            btnConnect.TabIndex = 65;
            btnConnect.Text = "Connect";
            btnConnect.Click += btnConnect_Click;
            // 
            // dbConnectionProfile
            // 
            dbConnectionProfile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dbConnectionProfile.DatabaseType = DatabaseInterpreter.Model.DatabaseType.Unknown;
            dbConnectionProfile.EnableDatabaseType = true;
            dbConnectionProfile.Location = new System.Drawing.Point(0, 5);
            dbConnectionProfile.Margin = new System.Windows.Forms.Padding(0);
            dbConnectionProfile.Name = "dbConnectionProfile";
            dbConnectionProfile.Size = new System.Drawing.Size(660, 27);
            dbConnectionProfile.TabIndex = 64;
            dbConnectionProfile.Title = "Database:";
            dbConnectionProfile.ProfileSelectedChanged += dbConnectionProfile_ProfileSelectedChanged;
            // 
            // txtMessage
            // 
            txtMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            txtMessage.Location = new System.Drawing.Point(0, 0);
            txtMessage.Multiline = true;
            txtMessage.Name = "txtMessage";
            txtMessage.Size = new System.Drawing.Size(752, 73);
            txtMessage.TabIndex = 61;
            // 
            // frmCodeGenerator
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(752, 537);
            Controls.Add(splitContainer1);
            Name = "frmCodeGenerator";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Code Generator";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private System.Windows.Forms.FolderBrowserDialog dlgOutputFolder;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private AntdUI.Checkbox chkGenerateComments;
        private AntdUI.Input txtNamespance;
        private AntdUI.Label lblNamespace;
        private AntdUI.Label label1;
        private AntdUI.Select cboLanguage;
        private AntdUI.Label lblOutputFolder;
        private AntdUI.Button btnOutputFolder;
        private AntdUI.Input txtOutputFolder;
        private AntdUI.Button btnCancel;
        private AntdUI.Button btnGenerate;
        private Controls.UC_DbObjectsSimpleTree tvDbObjects;
        private AntdUI.Button btnConnect;
        private Controls.UC_DbConnectionProfile dbConnectionProfile;
        private AntdUI.Input txtMessage;
    }
}