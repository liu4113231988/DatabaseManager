using DatabaseManager.Controls;

namespace DatabaseManager.Forms
{
    partial class frmGenerateColumnDocumentation
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
            btnSelectPath = new AntdUI.Button();
            txtFilePath = new AntdUI.Input();
            label2 = new AntdUI.Label();
            saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            chkShowTableComment = new AntdUI.Checkbox();
            gbProperties = new AntdUI.Panel();
            dgvProperties = new DraggableDataGridView();
            colSelect = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            colPropertyName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colDisplayName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            txtMessage = new AntdUI.Input();
            btnCancel = new AntdUI.Button();
            btnOK = new AntdUI.Button();
            colorDialog1 = new System.Windows.Forms.ColorDialog();
            label1 = new AntdUI.Label();
            txtBackColor = new AntdUI.Input();
            btnSelectBackColor = new AntdUI.Button();
            gbTableStyle = new AntdUI.Panel();
            chkColumnHeaderIsBold = new AntdUI.Checkbox();
            label3 = new AntdUI.Label();
            btnSelectForeColor = new AntdUI.Button();
            txtForeColor = new AntdUI.Input();
            gbProperties.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvProperties).BeginInit();
            gbTableStyle.SuspendLayout();
            SuspendLayout();
            // 
            // btnSelectPath
            // 
            btnSelectPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnSelectPath.Location = new System.Drawing.Point(498, 11);
            btnSelectPath.Name = "btnSelectPath";
            btnSelectPath.Size = new System.Drawing.Size(40, 32);
            btnSelectPath.TabIndex = 13;
            btnSelectPath.Text = "...";
            btnSelectPath.Click += btnSelectPath_Click;
            // 
            // txtFilePath
            // 
            txtFilePath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtFilePath.Location = new System.Drawing.Point(76, 12);
            txtFilePath.Name = "txtFilePath";
            txtFilePath.Size = new System.Drawing.Size(416, 32);
            txtFilePath.TabIndex = 12;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(3, 15);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(59, 17);
            label2.TabIndex = 11;
            label2.Text = "File Path:";
            // 
            // chkShowTableComment
            // 
            chkShowTableComment.AutoSize = true;
            chkShowTableComment.Checked = true;
            chkShowTableComment.Location = new System.Drawing.Point(16, 421);
            chkShowTableComment.Name = "chkShowTableComment";
            chkShowTableComment.Size = new System.Drawing.Size(149, 21);
            chkShowTableComment.TabIndex = 14;
            chkShowTableComment.Text = "Show table comment";
            // 
            // gbProperties
            // 
            gbProperties.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbProperties.Controls.Add(dgvProperties);
            gbProperties.Location = new System.Drawing.Point(5, 42);
            gbProperties.Name = "gbProperties";
            gbProperties.Size = new System.Drawing.Size(535, 229);
            gbProperties.TabIndex = 15;
            gbProperties.Text = "Properties to generate";
            // 
            // dgvProperties
            // 
            dgvProperties.AllowDrop = true;
            dgvProperties.AllowUserToAddRows = false;
            dgvProperties.AllowUserToDeleteRows = false;
            dgvProperties.AllowUserToOrderColumns = true;
            dgvProperties.BackgroundColor = System.Drawing.Color.White;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.LightGray;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dgvProperties.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dgvProperties.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvProperties.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { colSelect, colPropertyName, colDisplayName });
            dgvProperties.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvProperties.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            dgvProperties.Location = new System.Drawing.Point(3, 19);
            dgvProperties.Name = "dgvProperties";
            dgvProperties.RowHeadersWidth = 25;
            dgvProperties.Size = new System.Drawing.Size(529, 207);
            dgvProperties.TabIndex = 0;
            // 
            // colSelect
            // 
            colSelect.HeaderText = "";
            colSelect.Name = "colSelect";
            colSelect.Width = 40;
            // 
            // colPropertyName
            // 
            colPropertyName.HeaderText = "Property Name";
            colPropertyName.Name = "colPropertyName";
            colPropertyName.ReadOnly = true;
            colPropertyName.Width = 220;
            // 
            // colDisplayName
            // 
            colDisplayName.HeaderText = "Display Name";
            colDisplayName.Name = "colDisplayName";
            colDisplayName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            colDisplayName.Width = 220;
            // 
            // txtMessage
            // 
            txtMessage.BackColor = System.Drawing.Color.White;
            txtMessage.Dock = System.Windows.Forms.DockStyle.Bottom;
            txtMessage.Location = new System.Drawing.Point(0, 514);
            txtMessage.Multiline = true;
            txtMessage.Name = "txtMessage";
            txtMessage.ReadOnly = true;
            txtMessage.Size = new System.Drawing.Size(540, 58);
            txtMessage.TabIndex = 18;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnCancel.Enabled = false;
            btnCancel.Location = new System.Drawing.Point(267, 480);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(75, 32);
            btnCancel.TabIndex = 17;
            btnCancel.Text = "Cancel";
            btnCancel.Click += btnCancel_Click;
            // 
            // btnOK
            // 
            btnOK.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            btnOK.Location = new System.Drawing.Point(156, 480);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(75, 32);
            btnOK.TabIndex = 16;
            btnOK.Text = "OK";
            btnOK.Click += btnOK_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(7, 30);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(209, 17);
            label1.TabIndex = 19;
            label1.Text = "Column header background color:";
            // 
            // txtBackColor
            // 
            txtBackColor.BackColor = System.Drawing.Color.White;
            txtBackColor.Location = new System.Drawing.Point(222, 27);
            txtBackColor.Name = "txtBackColor";
            txtBackColor.Size = new System.Drawing.Size(100, 32);
            txtBackColor.TabIndex = 20;
            // 
            // btnSelectBackColor
            // 
            btnSelectBackColor.Location = new System.Drawing.Point(328, 27);
            btnSelectBackColor.Name = "btnSelectBackColor";
            btnSelectBackColor.Size = new System.Drawing.Size(75, 32);
            btnSelectBackColor.TabIndex = 21;
            btnSelectBackColor.Text = "Select";
            btnSelectBackColor.Click += btnSelectBackColor_Click;
            // 
            // gbTableStyle
            // 
            gbTableStyle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            gbTableStyle.Controls.Add(chkColumnHeaderIsBold);
            gbTableStyle.Controls.Add(label3);
            gbTableStyle.Controls.Add(btnSelectForeColor);
            gbTableStyle.Controls.Add(txtForeColor);
            gbTableStyle.Controls.Add(label1);
            gbTableStyle.Controls.Add(btnSelectBackColor);
            gbTableStyle.Controls.Add(txtBackColor);
            gbTableStyle.Location = new System.Drawing.Point(6, 280);
            gbTableStyle.Name = "gbTableStyle";
            gbTableStyle.Size = new System.Drawing.Size(531, 129);
            gbTableStyle.TabIndex = 22;
            gbTableStyle.Text = "Table style";
            // 
            // chkColumnHeaderIsBold
            // 
            chkColumnHeaderIsBold.AutoSize = true;
            chkColumnHeaderIsBold.Checked = true;
            chkColumnHeaderIsBold.Location = new System.Drawing.Point(11, 96);
            chkColumnHeaderIsBold.Name = "chkColumnHeaderIsBold";
            chkColumnHeaderIsBold.Size = new System.Drawing.Size(187, 21);
            chkColumnHeaderIsBold.TabIndex = 25;
            chkColumnHeaderIsBold.Text = "Column header font is bold";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(7, 66);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(159, 17);
            label3.TabIndex = 22;
            label3.Text = "Column header text color:";
            // 
            // btnSelectForeColor
            // 
            btnSelectForeColor.Location = new System.Drawing.Point(328, 63);
            btnSelectForeColor.Name = "btnSelectForeColor";
            btnSelectForeColor.Size = new System.Drawing.Size(75, 32);
            btnSelectForeColor.TabIndex = 24;
            btnSelectForeColor.Text = "Select";
            btnSelectForeColor.Click += btnSelectForeColor_Click;
            // 
            // txtForeColor
            // 
            txtForeColor.BackColor = System.Drawing.Color.White;
            txtForeColor.Location = new System.Drawing.Point(222, 63);
            txtForeColor.Name = "txtForeColor";
            txtForeColor.Size = new System.Drawing.Size(100, 32);
            txtForeColor.TabIndex = 23;
            // 
            // frmGenerateColumnDocumentation
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(540, 572);
            Controls.Add(gbTableStyle);
            Controls.Add(txtMessage);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(gbProperties);
            Controls.Add(chkShowTableComment);
            Controls.Add(btnSelectPath);
            Controls.Add(txtFilePath);
            Controls.Add(label2);
            Name = "frmGenerateColumnDocumentation";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Generate Column Documentation";
            Load += frmGenerateColumnDocumentation_Load;
            gbProperties.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvProperties).EndInit();
            gbTableStyle.ResumeLayout(false);
            gbTableStyle.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private AntdUI.Button btnSelectPath;
        private AntdUI.Input txtFilePath;
        private AntdUI.Label label2;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private AntdUI.Checkbox chkShowTableComment;
        private AntdUI.Panel gbProperties;
        private DraggableDataGridView dgvProperties;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelect;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPropertyName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDisplayName;
        private AntdUI.Input txtMessage;
        private AntdUI.Button btnCancel;
        private AntdUI.Button btnOK;
        private System.Windows.Forms.ColorDialog colorDialog1;
        private AntdUI.Label label1;
        private AntdUI.Input txtBackColor;
        private AntdUI.Button btnSelectBackColor;
        private AntdUI.Panel gbTableStyle;
        private AntdUI.Label label3;
        private AntdUI.Button btnSelectForeColor;
        private AntdUI.Input txtForeColor;
        private AntdUI.Checkbox chkColumnHeaderIsBold;
    }
}