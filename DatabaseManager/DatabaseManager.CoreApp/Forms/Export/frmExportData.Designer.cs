using DatabaseManager.Controls;

namespace DatabaseManager.Forms
{
    partial class frmExportData
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            label1 = new AntdUI.Label();
            cboFileType = new AntdUI.Select();
            chkShowColumnName = new AntdUI.Checkbox();
            btnOK = new AntdUI.Button();
            btnCancel = new AntdUI.Button();
            saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            label2 = new AntdUI.Label();
            txtFilePath = new AntdUI.Input();
            btnSelectPath = new AntdUI.Button();
            gbScope = new AntdUI.Panel();
            txtPageNumberRange = new AntdUI.Input();
            rbPageNumberRange = new AntdUI.Radio();
            rbAllPages = new AntdUI.Radio();
            rbCurrentPage = new AntdUI.Radio();
            txtMessage = new AntdUI.Input();
            gbColumns = new AntdUI.Panel();
            dgvColumns = new DraggableDataGridView();
            colSelect = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            colColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colDisplayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            gbScope.SuspendLayout();
            gbColumns.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvColumns).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(6, 9);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(62, 17);
            label1.TabIndex = 0;
            label1.Text = "File Type:";
            // 
            // cboFileType
            // 
            cboFileType.Location = new System.Drawing.Point(78, 6);
            cboFileType.Name = "cboFileType";
            cboFileType.Size = new System.Drawing.Size(152, 32);
            cboFileType.TabIndex = 1;
            // 
            // chkShowColumnName
            // 
            chkShowColumnName.AutoSize = true;
            chkShowColumnName.Location = new System.Drawing.Point(78, 78);
            chkShowColumnName.Name = "chkShowColumnName";
            chkShowColumnName.Size = new System.Drawing.Size(175, 21);
            chkShowColumnName.TabIndex = 5;
            chkShowColumnName.Text = "Show column name in file";
            chkShowColumnName.CheckedChanged += (sender, e) => chkShowColumnName_CheckedChanged(chkShowColumnName.Checked);
            // 
            // btnOK
            // 
            btnOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnOK.Location = new System.Drawing.Point(135, 510);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(75, 32);
            btnOK.TabIndex = 6;
            btnOK.Text = "OK";
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnCancel.Enabled = false;
            btnCancel.Location = new System.Drawing.Point(246, 510);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 32);
            btnCancel.TabIndex = 7;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(5, 41);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(59, 17);
            label2.TabIndex = 8;
            label2.Text = "File Path:";
            // 
            // txtFilePath
            // 
            txtFilePath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtFilePath.Location = new System.Drawing.Point(78, 38);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new System.Drawing.Size(402, 32);
            txtFilePath.TabIndex = 9;
            // 
            // btnSelectPath
            // 
            btnSelectPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnSelectPath.Location = new System.Drawing.Point(486, 37);
            btnSelectPath.Name = "btnSelectPath";
            btnSelectPath.Size = new System.Drawing.Size(40, 32);
            btnSelectPath.TabIndex = 10;
            btnSelectPath.Text = "...";
            btnSelectPath.Click += btnSelectPath_Click;
            // 
            // gbScope
            // 
            gbScope.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbScope.Controls.Add(txtPageNumberRange);
            gbScope.Controls.Add(rbPageNumberRange);
            gbScope.Controls.Add(rbAllPages);
            gbScope.Controls.Add(rbCurrentPage);
            gbScope.Location = new System.Drawing.Point(12, 390);
            gbScope.Name = "gbScope";
            gbScope.Size = new System.Drawing.Size(515, 114);
            gbScope.TabIndex = 11;
            gbScope.Text = "Scope";
            // 
            // txtPageNumberRange
            // 
            txtPageNumberRange.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtPageNumberRange.Enabled = false;
            txtPageNumberRange.Location = new System.Drawing.Point(89, 83);
            txtPageNumberRange.Name = "txtPageNumberRange";
            txtPageNumberRange.PlaceholderText = "for example: 1-3,5,9";
            txtPageNumberRange.Size = new System.Drawing.Size(417, 32);
            txtPageNumberRange.TabIndex = 3;
            // 
            // rbPageNumberRange
            // 
            rbPageNumberRange.AutoSize = true;
            rbPageNumberRange.Location = new System.Drawing.Point(16, 83);
            rbPageNumberRange.Name = "rbPageNumberRange";
            rbPageNumberRange.Size = new System.Drawing.Size(63, 21);
            rbPageNumberRange.TabIndex = 2;
            rbPageNumberRange.Text = "Range";
            rbPageNumberRange.CheckedChanged += (sender, e) => rbPageNumberRange_CheckedChanged(rbPageNumberRange.Checked);
            // 
            // rbAllPages
            // 
            rbAllPages.AutoSize = true;
            rbAllPages.Location = new System.Drawing.Point(16, 51);
            rbAllPages.Name = "rbAllPages";
            rbAllPages.Size = new System.Drawing.Size(80, 21);
            rbAllPages.TabIndex = 1;
            rbAllPages.Text = "All pages";
            // 
            // rbCurrentPage
            // 
            rbCurrentPage.AutoSize = true;
            rbCurrentPage.Checked = true;
            rbCurrentPage.Location = new System.Drawing.Point(16, 22);
            rbCurrentPage.Name = "rbCurrentPage";
            rbCurrentPage.Size = new System.Drawing.Size(103, 21);
            rbCurrentPage.TabIndex = 0;
            rbCurrentPage.Text = "Current page";
            // 
            // txtMessage
            // 
            txtMessage.BackColor = System.Drawing.Color.White;
            txtMessage.Dock = System.Windows.Forms.DockStyle.Bottom;
            txtMessage.Location = new System.Drawing.Point(0, 545);
            txtMessage.Multiline = true;
            txtMessage.Name = "txtMessage";
            txtMessage.ReadOnly = true;
            txtMessage.Size = new System.Drawing.Size(530, 58);
            txtMessage.TabIndex = 12;
            // 
            // gbColumns
            // 
            gbColumns.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbColumns.Controls.Add(dgvColumns);
            gbColumns.Location = new System.Drawing.Point(6, 106);
            gbColumns.Name = "gbColumns";
            gbColumns.Size = new System.Drawing.Size(521, 281);
            gbColumns.TabIndex = 13;
            gbColumns.Text = "Columns to export";
            // 
            // dgvColumns
            // 
            dgvColumns.AllowDrop = true;
            dgvColumns.AllowUserToAddRows = false;
            dgvColumns.AllowUserToDeleteRows = false;
            dgvColumns.AllowUserToOrderColumns = true;
            dgvColumns.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightGray;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvColumns.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvColumns.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvColumns.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { colSelect, colColumnName, colDisplayName });
            dgvColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvColumns.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            dgvColumns.Location = new System.Drawing.Point(3, 19);
            dgvColumns.Name = "dgvColumns";
            dgvColumns.RowHeadersWidth = 25;
            dgvColumns.Size = new System.Drawing.Size(515, 259);
            dgvColumns.TabIndex = 0;
            // colSelect
            // 
            colSelect.HeaderText = "";
            colSelect.Name = "colSelect";
            colSelect.Width = 40;
            // 
            // colColumnName
            // 
            colColumnName.HeaderText = "Column Name";
            colColumnName.Name = "colColumnName";
            colColumnName.ReadOnly = true;
            colColumnName.Width = 220;
            // 
            // colDisplayName
            // 
            colDisplayName.HeaderText = "Display Name";
            colDisplayName.Name = "colDisplayName";
            colDisplayName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            colDisplayName.Visible = false;
            colDisplayName.Width = 220;
            // 
            // frmExportData
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(530, 603);
            Controls.Add(gbColumns);
            Controls.Add(txtMessage);
            Controls.Add(gbScope);
            Controls.Add(btnSelectPath);
            Controls.Add(txtFilePath);
            Controls.Add(label2);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(chkShowColumnName);
            Controls.Add(cboFileType);
            Controls.Add(label1);
            Name = "frmExportData";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Export Data";
            Load += frmExportData_Load;
            gbScope.ResumeLayout(false);
            gbScope.PerformLayout();
            gbColumns.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvColumns).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private AntdUI.Label label1;
        private AntdUI.Select cboFileType;
        private AntdUI.Checkbox chkShowColumnName;
        private AntdUI.Button btnOK;
        private AntdUI.Button btnCancel;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private AntdUI.Label label2;
        private AntdUI.Input txtFilePath;
        private AntdUI.Button btnSelectPath;
        private AntdUI.Panel gbScope;
        private AntdUI.Radio rbAllPages;
        private AntdUI.Radio rbCurrentPage;
        private AntdUI.Input txtMessage;
        private AntdUI.Panel gbColumns;
        private DraggableDataGridView dgvColumns;
        private AntdUI.Radio rbPageNumberRange;
        private AntdUI.Input txtPageNumberRange;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelect;
        private System.Windows.Forms.DataGridViewTextBoxColumn colColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDisplayName;
    }
}