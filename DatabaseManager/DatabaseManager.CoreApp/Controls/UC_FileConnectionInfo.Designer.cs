namespace DatabaseManager.Controls
{
    partial class UC_FileConnectionInfo
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new AntdUI.Label();
            this.label2 = new AntdUI.Label();
            this.txtFilePath = new AntdUI.Input();
            this.txtPassword = new AntdUI.Input();
            this.btnBrowserFile = new AntdUI.Button();
            this.btnTest = new AntdUI.Button();
            this.chkRememberPassword = new AntdUI.Checkbox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.chkHasPassword = new AntdUI.Checkbox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "File path:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(3, 79);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(67, 17);
            this.label2.TabIndex = 1;
            this.label2.Text = "Password:";
            // 
            // txtFilePath
            // 
            this.txtFilePath.Location = new System.Drawing.Point(101, 16);
            this.txtFilePath.Name = "txtFilePath";
            this.txtFilePath.Size = new System.Drawing.Size(187, 32);
            this.txtFilePath.TabIndex = 2;
            // 
            // txtPassword
            // 
            this.txtPassword.Enabled = false;
            this.txtPassword.Location = new System.Drawing.Point(101, 76);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(187, 32);
            this.txtPassword.TabIndex = 3;
            // 
            // btnBrowserFile
            // 
            this.btnBrowserFile.Location = new System.Drawing.Point(294, 16);
            this.btnBrowserFile.Name = "btnBrowserFile";
            this.btnBrowserFile.Size = new System.Drawing.Size(50, 32);
            this.btnBrowserFile.TabIndex = 4;
            this.btnBrowserFile.Text = "...";
            this.btnBrowserFile.Click += new System.EventHandler(this.btnBrowserFile_Click);
            // 
            // btnTest
            // 
            this.btnTest.Location = new System.Drawing.Point(294, 76);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(50, 32);
            this.btnTest.TabIndex = 5;
            this.btnTest.Text = "Test";
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // chkRememberPassword
            // 
            this.chkRememberPassword.AutoSize = true;
            this.chkRememberPassword.Enabled = false;
            this.chkRememberPassword.Location = new System.Drawing.Point(101, 113);
            this.chkRememberPassword.Name = "chkRememberPassword";
            this.chkRememberPassword.Size = new System.Drawing.Size(152, 21);
            this.chkRememberPassword.TabIndex = 6;
            this.chkRememberPassword.Text = "Remember password";
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // chkHasPassword
            // 
            this.chkHasPassword.AutoSize = true;
            this.chkHasPassword.Location = new System.Drawing.Point(101, 48);
            this.chkHasPassword.Name = "chkHasPassword";
            this.chkHasPassword.Size = new System.Drawing.Size(110, 21);
            this.chkHasPassword.TabIndex = 7;
            this.chkHasPassword.Text = "Has password";
            this.chkHasPassword.CheckedChanged += (sender, e) => this.chkHasPassword_CheckedChanged(sender, new System.EventArgs());
            // 
            // UC_FileConnectionInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.chkHasPassword);
            this.Controls.Add(this.chkRememberPassword);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.btnBrowserFile);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.txtFilePath);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "UC_FileConnectionInfo";
            this.Size = new System.Drawing.Size(358, 141);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private AntdUI.Label label1;
        private AntdUI.Label label2;
        private AntdUI.Input txtFilePath;
        private AntdUI.Input txtPassword;
        private AntdUI.Button btnBrowserFile;
        private AntdUI.Button btnTest;
        private AntdUI.Checkbox chkRememberPassword;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private AntdUI.Checkbox chkHasPassword;
    }
}
