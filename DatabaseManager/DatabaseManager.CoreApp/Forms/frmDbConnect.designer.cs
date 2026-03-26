namespace DatabaseManager.Forms
{
    partial class frmDbConnect
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
            btnConfirm = new AntdUI.Button();
            lblDatabase = new AntdUI.Label();
            cboDatabase = new AntdUI.Select();
            lblProfileName = new AntdUI.Label();
            txtProfileName = new AntdUI.Input();
            btnCancel = new AntdUI.Button();
            label2 = new AntdUI.Label();
            rbInput = new AntdUI.Radio();
            rbChoose = new AntdUI.Radio();
            ucDbAccountInfo = new Controls.UC_DbAccountInfo();
            panelMode = new AntdUI.Panel();
            panelContent = new AntdUI.Panel();
            panelButton = new AntdUI.Panel();
            panelMode.SuspendLayout();
            panelContent.SuspendLayout();
            panelButton.SuspendLayout();
            SuspendLayout();
            // 
            // btnConfirm
            // 
            btnConfirm.Location = new System.Drawing.Point(141, 7);
            btnConfirm.Margin = new System.Windows.Forms.Padding(4);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new System.Drawing.Size(88, 36);
            btnConfirm.TabIndex = 9;
            btnConfirm.Text = "OK";
            btnConfirm.Click += btnConfirm_Click;
            // 
            // lblDatabase
            // 
            lblDatabase.AutoSize = true;
            lblDatabase.Location = new System.Drawing.Point(15, 249);
            lblDatabase.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblDatabase.Name = "lblDatabase";
            lblDatabase.Size = new System.Drawing.Size(66, 17);
            lblDatabase.TabIndex = 11;
            lblDatabase.Text = "Database:";
            // 
            // cboDatabase
            // 
            cboDatabase.Location = new System.Drawing.Point(143, 244);
            cboDatabase.Margin = new System.Windows.Forms.Padding(4);
            cboDatabase.Name = "cboDatabase";
            cboDatabase.Size = new System.Drawing.Size(193, 32);
            cboDatabase.TabIndex = 7;
            cboDatabase.SelectedIndexChanged += (sender, index) => cboDatabase_SelectedIndexChanged(sender, new System.EventArgs());
            // 
            // lblProfileName
            // 
            lblProfileName.AutoSize = true;
            lblProfileName.Location = new System.Drawing.Point(15, 291);
            lblProfileName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            lblProfileName.Name = "lblProfileName";
            lblProfileName.Size = new System.Drawing.Size(84, 17);
            lblProfileName.TabIndex = 16;
            lblProfileName.Text = "Profile name:";
            // 
            // txtProfileName
            // 
            txtProfileName.Location = new System.Drawing.Point(142, 287);
            txtProfileName.Margin = new System.Windows.Forms.Padding(4);
            txtProfileName.Name = "txtProfileName";
            txtProfileName.Size = new System.Drawing.Size(193, 32);
            txtProfileName.TabIndex = 0;
            // 
            // btnCancel
            // 
            btnCancel.Location = new System.Drawing.Point(247, 7);
            btnCancel.Margin = new System.Windows.Forms.Padding(4);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(88, 36);
            btnCancel.TabIndex = 17;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(8, 11);
            label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(46, 17);
            label2.TabIndex = 19;
            label2.Text = "Mode:";
            // 
            // rbInput
            // 
            rbInput.AutoSize = true;
            rbInput.Checked = true;
            rbInput.Location = new System.Drawing.Point(131, 9);
            rbInput.Margin = new System.Windows.Forms.Padding(4);
            rbInput.Name = "rbInput";
            rbInput.Size = new System.Drawing.Size(56, 21);
            rbInput.TabIndex = 20;
            rbInput.Text = "Input";
            // 
            // rbChoose
            // 
            rbChoose.AutoSize = true;
            rbChoose.Location = new System.Drawing.Point(254, 9);
            rbChoose.Margin = new System.Windows.Forms.Padding(4);
            rbChoose.Name = "rbChoose";
            rbChoose.Size = new System.Drawing.Size(70, 21);
            rbChoose.TabIndex = 21;
            rbChoose.Text = "Choose";
            rbChoose.CheckedChanged += (sender, value) => rbChoose_CheckedChanged(sender, new System.EventArgs());
            // 
            // ucDbAccountInfo
            // 
            ucDbAccountInfo.DatabaseType = DatabaseInterpreter.Model.DatabaseType.SqlServer;
            ucDbAccountInfo.Location = new System.Drawing.Point(5, 6);
            ucDbAccountInfo.Margin = new System.Windows.Forms.Padding(5, 6, 5, 6);
            ucDbAccountInfo.Name = "ucDbAccountInfo";
            ucDbAccountInfo.Size = new System.Drawing.Size(435, 230);
            ucDbAccountInfo.TabIndex = 1;
            // 
            // panelMode
            // 
            panelMode.Controls.Add(rbChoose);
            panelMode.Controls.Add(label2);
            panelMode.Controls.Add(rbInput);
            panelMode.Location = new System.Drawing.Point(12, 3);
            panelMode.Name = "panelMode";
            panelMode.Size = new System.Drawing.Size(392, 39);
            panelMode.TabIndex = 22;
            // 
            // panelContent
            // 
            panelContent.Controls.Add(ucDbAccountInfo);
            panelContent.Controls.Add(lblDatabase);
            panelContent.Controls.Add(cboDatabase);
            panelContent.Controls.Add(lblProfileName);
            panelContent.Controls.Add(txtProfileName);
            panelContent.Location = new System.Drawing.Point(12, 48);
            panelContent.Name = "panelContent";
            panelContent.Size = new System.Drawing.Size(449, 320);
            panelContent.TabIndex = 23;
            // 
            // panelButton
            // 
            panelButton.Controls.Add(btnCancel);
            panelButton.Controls.Add(btnConfirm);
            panelButton.Location = new System.Drawing.Point(12, 379);
            panelButton.Name = "panelButton";
            panelButton.Size = new System.Drawing.Size(449, 44);
            panelButton.TabIndex = 24;
            // 
            // frmDbConnect
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(466, 435);
            Controls.Add(panelButton);
            Controls.Add(panelContent);
            Controls.Add(panelMode);
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "frmDbConnect";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Database Connection";
            Activated += frmDbConnect_Activated;
            Load += frmDbConnect_Load;
            panelMode.ResumeLayout(false);
            panelMode.PerformLayout();
            panelContent.ResumeLayout(false);
            panelContent.PerformLayout();
            panelButton.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion
        private AntdUI.Button btnConfirm;
        private AntdUI.Label lblDatabase;
        private AntdUI.Select cboDatabase;
        private AntdUI.Label lblProfileName;
        private AntdUI.Input txtProfileName;
        private Controls.UC_DbAccountInfo ucDbAccountInfo;
        private AntdUI.Button btnCancel;
        private AntdUI.Label label2;
        private AntdUI.Radio rbInput;
        private AntdUI.Radio rbChoose;
        private AntdUI.Panel panelMode;
        private AntdUI.Panel panelContent;
        private AntdUI.Panel panelButton;
    }
}