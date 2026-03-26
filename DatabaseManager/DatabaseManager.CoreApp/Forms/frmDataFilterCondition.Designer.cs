namespace DatabaseManager.Forms
{
    partial class frmDataFilterCondition
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
            this.btnOK = new AntdUI.Button();
            this.gbRange = new AntdUI.Panel();
            this.panelRange = new AntdUI.Panel();
            this.label2 = new AntdUI.Label();
            this.label3 = new AntdUI.Label();
            this.txtFrom = new AntdUI.Input();
            this.txtTo = new AntdUI.Input();
            this.rbRange = new AntdUI.Radio();
            this.btnCancel = new AntdUI.Button();
            this.btnClear = new AntdUI.Button();
            this.gbSeries = new AntdUI.Panel();
            this.panelSeries = new AntdUI.Panel();
            this.txtValues = new AntdUI.Input();
            this.gbSingle = new AntdUI.Panel();
            this.panelSingle = new AntdUI.Panel();
            this.txtValue = new AntdUI.Input();
            this.cboOperator = new AntdUI.Select();
            this.rbSingle = new AntdUI.Radio();
            this.rbSeries = new AntdUI.Radio();
            this.gbRange.SuspendLayout();
            this.panelRange.SuspendLayout();
            this.gbSeries.SuspendLayout();
            this.panelSeries.SuspendLayout();
            this.gbSingle.SuspendLayout();
            this.panelSingle.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(284, 287);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(70, 22);
            this.btnOK.TabIndex = 12;
            this.btnOK.Text = "OK";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // gbRange
            // 
            this.gbRange.Controls.Add(this.panelRange);
            this.gbRange.Location = new System.Drawing.Point(12, 105);
            this.gbRange.Name = "gbRange";
            this.gbRange.Size = new System.Drawing.Size(417, 56);
            this.gbRange.TabIndex = 10;
            this.gbRange.Text = "";
            // 
            // panelRange
            // 
            this.panelRange.Controls.Add(this.label2);
            this.panelRange.Controls.Add(this.label3);
            this.panelRange.Controls.Add(this.txtFrom);
            this.panelRange.Controls.Add(this.txtTo);
            this.panelRange.Enabled = false;
            this.panelRange.Location = new System.Drawing.Point(11, 20);
            this.panelRange.Name = "panelRange";
            this.panelRange.Size = new System.Drawing.Size(405, 30);
            this.panelRange.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "Between";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(213, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(23, 12);
            this.label3.TabIndex = 2;
            this.label3.Text = "and";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtFrom
            // 
            this.txtFrom.Location = new System.Drawing.Point(66, 6);
            this.txtFrom.Name = "txtFrom";
            this.txtFrom.Size = new System.Drawing.Size(141, 21);
            this.txtFrom.TabIndex = 1;
            // 
            // txtTo
            // 
            this.txtTo.Location = new System.Drawing.Point(242, 6);
            this.txtTo.Name = "txtTo";
            this.txtTo.Size = new System.Drawing.Size(157, 21);
            this.txtTo.TabIndex = 3;
            // 
            // rbRange
            // 
            this.rbRange.AutoSize = true;
            this.rbRange.Location = new System.Drawing.Point(17, 87);
            this.rbRange.Name = "rbRange";
            this.rbRange.Size = new System.Drawing.Size(125, 16);
            this.rbRange.TabIndex = 4;
            this.rbRange.Text = "Interval criteria";
            this.rbRange.CheckedChanged += new AntdUI.BoolEventHandler(this.rbRange_CheckedChanged);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(360, 287);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(70, 22);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnClear
            // 
            this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClear.Location = new System.Drawing.Point(22, 287);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(70, 22);
            this.btnClear.TabIndex = 11;
            this.btnClear.Text = "Clear";
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // gbSeries
            // 
            this.gbSeries.Controls.Add(this.panelSeries);
            this.gbSeries.Location = new System.Drawing.Point(12, 195);
            this.gbSeries.Name = "gbSeries";
            this.gbSeries.Size = new System.Drawing.Size(417, 56);
            this.gbSeries.TabIndex = 14;
            this.gbSeries.Text = "";
            // 
            // panelSeries
            // 
            this.panelSeries.Controls.Add(this.txtValues);
            this.panelSeries.Enabled = false;
            this.panelSeries.Location = new System.Drawing.Point(11, 20);
            this.panelSeries.Name = "panelSeries";
            this.panelSeries.Size = new System.Drawing.Size(405, 30);
            this.panelSeries.TabIndex = 3;
            // 
            // txtValues
            // 
            this.txtValues.Location = new System.Drawing.Point(5, 6);
            this.txtValues.Name = "txtValues";
            this.txtValues.Size = new System.Drawing.Size(394, 21);
            this.txtValues.TabIndex = 1;
            // 
            // gbSingle
            // 
            this.gbSingle.Controls.Add(this.panelSingle);
            this.gbSingle.Location = new System.Drawing.Point(12, 20);
            this.gbSingle.Name = "gbSingle";
            this.gbSingle.Size = new System.Drawing.Size(417, 56);
            this.gbSingle.TabIndex = 8;
            this.gbSingle.Text = "";
            // 
            // panelSingle
            // 
            this.panelSingle.Controls.Add(this.txtValue);
            this.panelSingle.Controls.Add(this.cboOperator);
            this.panelSingle.Location = new System.Drawing.Point(6, 20);
            this.panelSingle.Name = "panelSingle";
            this.panelSingle.Size = new System.Drawing.Size(410, 27);
            this.panelSingle.TabIndex = 3;
            // 
            // txtValue
            // 
            this.txtValue.Location = new System.Drawing.Point(72, 3);
            this.txtValue.Name = "txtValue";
            this.txtValue.Size = new System.Drawing.Size(333, 21);
            this.txtValue.TabIndex = 1;
            // 
            // cboOperator
            // 
            this.cboOperator.Items.AddRange(new object[] {
            "=",
            ">",
            ">=",
            "<",
            "<=",
            "<>",
            "LIKE",
            "NOT LIKE"});
            this.cboOperator.Location = new System.Drawing.Point(10, 3);
            this.cboOperator.Name = "cboOperator";
            this.cboOperator.Size = new System.Drawing.Size(56, 20);
            this.cboOperator.TabIndex = 0;
            // 
            // rbSingle
            // 
            this.rbSingle.AutoSize = true;
            this.rbSingle.Checked = true;
            this.rbSingle.Location = new System.Drawing.Point(17, 6);
            this.rbSingle.Name = "rbSingle";
            this.rbSingle.Size = new System.Drawing.Size(149, 16);
            this.rbSingle.TabIndex = 2;
            this.rbSingle.Text = "Single value criteria";
            this.rbSingle.CheckedChanged += new AntdUI.BoolEventHandler(this.rbSingle_CheckedChanged);
            // 
            // rbSeries
            // 
            this.rbSeries.AutoSize = true;
            this.rbSeries.Location = new System.Drawing.Point(17, 183);
            this.rbSeries.Name = "rbSeries";
            this.rbSeries.Size = new System.Drawing.Size(59, 16);
            this.rbSeries.TabIndex = 15;
            this.rbSeries.Text = "Series";
            // 
            // frmDataFilterCondition
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(436, 328);
            this.Controls.Add(this.rbSeries);
            this.Controls.Add(this.gbSeries);
            this.Controls.Add(this.rbRange);
            this.Controls.Add(this.rbSingle);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.gbSingle);
            this.Controls.Add(this.gbRange);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnClear);
            this.MaximizeBox = false;
            this.Name = "frmDataFilterCondition";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Query Condition";
            this.Load += new System.EventHandler(this.frmDataFilterCondition_Load);
            this.gbRange.ResumeLayout(false);
            this.panelRange.ResumeLayout(false);
            this.panelRange.PerformLayout();
            this.gbSeries.ResumeLayout(false);
            this.panelSeries.ResumeLayout(false);
            this.panelSeries.PerformLayout();
            this.gbSingle.ResumeLayout(false);
            this.panelSingle.ResumeLayout(false);
            this.panelSingle.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private AntdUI.Button btnOK;
        private AntdUI.Panel gbRange;
        private AntdUI.Label label2;
        private AntdUI.Input txtFrom;
        private AntdUI.Input txtTo;
        private AntdUI.Label label3;
        private AntdUI.Button btnCancel;
        private AntdUI.Button btnClear;
        private AntdUI.Panel gbSeries;
        private AntdUI.Input txtValues;
        private AntdUI.Radio rbRange;
        private AntdUI.Panel panelRange;
        private AntdUI.Panel panelSeries;
        private AntdUI.Panel gbSingle;
        private AntdUI.Panel panelSingle;
        private AntdUI.Input txtValue;
        private AntdUI.Select cboOperator;
        private AntdUI.Radio rbSingle;
        private AntdUI.Radio rbSeries;
    }
}