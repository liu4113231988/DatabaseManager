namespace DatabaseManager.Forms
{
    partial class frmTranslateScript
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmTranslateScript));
            cboTargetDbType = new AntdUI.Select();
            cboSourceDbType = new AntdUI.Select();
            lblSource = new AntdUI.Label();
            lblTarget = new AntdUI.Label();
            splitContainer1 = new System.Windows.Forms.SplitContainer();
            txtSource = new SqlCodeEditor.TextEditorControlEx();
            txtTarget = new SqlCodeEditor.TextEditorControlEx();
            btnTranlate = new AntdUI.Button();
            btnClose = new AntdUI.Button();
            btnClear = new AntdUI.Button();
            btnExchange = new AntdUI.Button();
            chkHighlighting = new AntdUI.Checkbox();
            chkValidateScriptsAfterTranslated = new AntdUI.Checkbox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // cboTargetDbType
            // 
            cboTargetDbType.Location = new System.Drawing.Point(342, 8);
            cboTargetDbType.Margin = new System.Windows.Forms.Padding(4);
            cboTargetDbType.Name = "cboTargetDbType";
            cboTargetDbType.Size = new System.Drawing.Size(124, 32);
            cboTargetDbType.TabIndex = 42;
            cboTargetDbType.SelectedIndexChanged += (sender, index) => cboTargetDbType_SelectedIndexChanged(sender, new System.EventArgs());
            // 
            // cboSourceDbType
            // 
            cboSourceDbType.Location = new System.Drawing.Point(69, 8);
            cboSourceDbType.Margin = new System.Windows.Forms.Padding(4);
            cboSourceDbType.Name = "cboSourceDbType";
            cboSourceDbType.Size = new System.Drawing.Size(124, 32);
            cboSourceDbType.TabIndex = 41;
            cboSourceDbType.SelectedIndexChanged += (sender, index) => cboSourceDbType_SelectedIndexChanged(sender, new System.EventArgs());
            // 
            // lblSource
            // 
            lblSource.AutoSize = true;
            lblSource.Location = new System.Drawing.Point(11, 11);
            lblSource.Name = "lblSource";
            lblSource.Size = new System.Drawing.Size(51, 17);
            lblSource.TabIndex = 43;
            lblSource.Text = "Source:";
            // 
            // lblTarget
            // 
            lblTarget.AutoSize = true;
            lblTarget.Location = new System.Drawing.Point(287, 11);
            lblTarget.Name = "lblTarget";
            lblTarget.Size = new System.Drawing.Size(49, 17);
            lblTarget.TabIndex = 44;
            lblTarget.Text = "Target:";
            // 
            // splitContainer1
            // 
            splitContainer1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            splitContainer1.Location = new System.Drawing.Point(0, 43);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(txtSource);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(txtTarget);
            splitContainer1.Size = new System.Drawing.Size(851, 396);
            splitContainer1.SplitterDistance = 400;
            splitContainer1.SplitterWidth = 10;
            splitContainer1.TabIndex = 45;
            // 
            // txtSource
            // 
            txtSource.ContextMenuEnabled = true;
            txtSource.ContextMenuShowDefaultIcons = true;
            txtSource.Dock = System.Windows.Forms.DockStyle.Fill;
            txtSource.EnableFolding = false;
            txtSource.FoldingStrategy = null;
            txtSource.Font = new System.Drawing.Font("Courier New", 10F);
            txtSource.Location = new System.Drawing.Point(0, 0);
            txtSource.Name = "txtSource";
            txtSource.ShowVRuler = false;
            txtSource.Size = new System.Drawing.Size(400, 396);
            txtSource.SyntaxHighlighting = "\"\"";
            txtSource.TabIndex = 0;
            txtSource.KeyDown += txtSource_KeyDown;
            txtSource.KeyUp += txtSource_KeyUp;
            // 
            // txtTarget
            // 
            txtTarget.ContextMenuEnabled = true;
            txtTarget.ContextMenuShowDefaultIcons = true;
            txtTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTarget.EnableFolding = false;
            txtTarget.FoldingStrategy = null;
            txtTarget.Font = new System.Drawing.Font("Courier New", 10F);
            txtTarget.Location = new System.Drawing.Point(0, 0);
            txtTarget.Name = "txtTarget";
            txtTarget.ShowVRuler = false;
            txtTarget.Size = new System.Drawing.Size(441, 396);
            txtTarget.SyntaxHighlighting = "\"\"";
            txtTarget.TabIndex = 0;
            // 
            // btnTranlate
            // 
            btnTranlate.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnTranlate.Location = new System.Drawing.Point(671, 445);
            btnTranlate.Name = "btnTranlate";
            btnTranlate.Size = new System.Drawing.Size(75, 36);
            btnTranlate.TabIndex = 46;
            btnTranlate.Text = "Translate";
            btnTranlate.Click += btnTranlate_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            btnClose.Location = new System.Drawing.Point(769, 445);
            btnClose.Name = "btnClose";
            btnClose.Size = new System.Drawing.Size(75, 36);
            btnClose.TabIndex = 47;
            btnClose.Text = "Close";
            btnClose.Click += btnClose_Click;
            // 
            // btnClear
            // 
            btnClear.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            btnClear.Location = new System.Drawing.Point(11, 445);
            btnClear.Name = "btnClear";
            btnClear.Size = new System.Drawing.Size(75, 36);
            btnClear.TabIndex = 48;
            btnClear.Text = "Clear";
            btnClear.Click += btnClear_Click;
            // 
            // btnExchange
            // 
            btnExchange.Location = new System.Drawing.Point(219, 8);
            btnExchange.Name = "btnExchange";
            btnExchange.Size = new System.Drawing.Size(49, 32);
            btnExchange.TabIndex = 49;
            btnExchange.Text = "⇄";
            btnExchange.Click += btnExchange_Click;
            // 
            // chkHighlighting
            // 
            chkHighlighting.AutoSize = true;
            chkHighlighting.Checked = true;
            chkHighlighting.Location = new System.Drawing.Point(491, 12);
            chkHighlighting.Name = "chkHighlighting";
            chkHighlighting.Size = new System.Drawing.Size(122, 21);
            chkHighlighting.TabIndex = 50;
            chkHighlighting.Text = "Highlighting text";
            chkHighlighting.CheckedChanged += (sender, value) => chkHighlighting_CheckedChanged(sender, new System.EventArgs());
            // 
            // chkValidateScriptsAfterTranslated
            // 
            chkValidateScriptsAfterTranslated.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            chkValidateScriptsAfterTranslated.AutoSize = true;
            chkValidateScriptsAfterTranslated.Location = new System.Drawing.Point(632, 13);
            chkValidateScriptsAfterTranslated.Name = "chkValidateScriptsAfterTranslated";
            chkValidateScriptsAfterTranslated.Size = new System.Drawing.Size(209, 21);
            chkValidateScriptsAfterTranslated.TabIndex = 51;
            chkValidateScriptsAfterTranslated.Text = "Validate scripts after translated";
            // 
            // frmTranslateScript
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(853, 489);
            Controls.Add(chkValidateScriptsAfterTranslated);
            Controls.Add(chkHighlighting);
            Controls.Add(btnExchange);
            Controls.Add(btnClear);
            Controls.Add(btnClose);
            Controls.Add(btnTranlate);
            Controls.Add(splitContainer1);
            Controls.Add(lblTarget);
            Controls.Add(lblSource);
            Controls.Add(cboTargetDbType);
            Controls.Add(cboSourceDbType);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Name = "frmTranslateScript";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Translate Script";
            Load += frmTranslateScript_Load;
            KeyDown += frmTranslateScript_KeyDown;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private AntdUI.Select cboTargetDbType;
        private AntdUI.Select cboSourceDbType;
        private AntdUI.Label lblSource;
        private AntdUI.Label lblTarget;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private AntdUI.Button btnTranlate;
        private AntdUI.Button btnClose;
        private AntdUI.Button btnClear;
        private AntdUI.Button btnExchange;
        private AntdUI.Checkbox chkHighlighting;
        private AntdUI.Checkbox chkValidateScriptsAfterTranslated;
        private SqlCodeEditor.TextEditorControlEx txtSource;
        private SqlCodeEditor.TextEditorControlEx txtTarget;
    }
}