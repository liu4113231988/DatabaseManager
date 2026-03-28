using DatabaseInterpreter.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using AntdUI;

namespace DatabaseManager.Forms
{
    public partial class frmColumnMapping : Form
    {
        private const string EmptyItem = "<None>";
        public string ReferenceTableName { get; set; }
        public string TableName { get; set; }
        public List<string> ReferenceTableColumns { get; set; } = new List<string>();
        public List<string> TableColumns { get; set; } = new List<string>();

        public List<ForeignKeyColumn> Mappings { get; set; } = new List<ForeignKeyColumn>();

        public frmColumnMapping()
        {
            InitializeComponent();
        }

        private void frmColumnMapping_Load(object sender, EventArgs e)
        {
            this.InitControls();
        }

        private void InitControls()
        {
            this.gbReferenceTable.Text = this.ReferenceTableName;
            this.gbTable.Text = this.TableName;

            this.LoadMappings();
        }

        private void LoadMappings()
        {
            if (this.Mappings != null)
            {
                foreach (ForeignKeyColumn mapping in this.Mappings)
                {
                    AntdUI.Select refCombo = this.CreateCombobox(this.panelReferenceTable, this.ReferenceTableColumns, mapping.ReferencedColumnName);

                    this.panelReferenceTable.Controls.Add(refCombo);

                    AntdUI.Select combo = this.CreateCombobox(this.panelTable, this.TableColumns, mapping.ColumnName);

                    this.panelTable.Controls.Add(combo);
                }
            }

            AntdUI.Select refComboEmpty = this.CreateCombobox(this.panelReferenceTable, this.ReferenceTableColumns, null);

            this.panelReferenceTable.Controls.Add(refComboEmpty);

            AntdUI.Select comboEmpty = this.CreateCombobox(this.panelTable, this.TableColumns, null);

            this.panelTable.Controls.Add(comboEmpty);
        }

        private AntdUI.Select CreateCombobox(AntdUI.Panel panel, List<string> values, string value)
        {
            AntdUI.Select combo = new AntdUI.Select();
            combo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            combo.Width = panelTable.Width - 5;
            combo.Tag = panel.Controls.Count + 1;
            combo.SelectedIndexChanged += (sender, index) => Combo_SelectedIndexChanged(sender, new EventArgs());

            var availableValues = this.GetValuesWithEmptyItem(values).Except(this.GetExistingValues(panel)).ToArray();
            foreach (var item in availableValues)
            {
                combo.Items.Add(item);
            }

            if (panel.Controls.Count > 0)
            {
                combo.Top = panel.Controls.Count * combo.Height + panel.Controls.Count;
            }

            if (!string.IsNullOrEmpty(value) && values.Contains(value))
            {
                combo.Text = value;
            }

            return combo;
        }

        private List<string> GetExistingValues(AntdUI.Panel panel)
        {
            List<string> values = new List<string>();
            var comboboxes = panel.Controls.OfType<AntdUI.Select>();

            foreach (AntdUI.Select combo in comboboxes)
            {
                if (!string.IsNullOrEmpty(combo.Text) && combo.Text != EmptyItem)
                {
                    values.Add(combo.Text);
                }
            }

            return values;
        }

        private List<string> GetValuesWithEmptyItem(List<string> values)
        {
            var cloneValues = values.Select(item => item).ToList();
            cloneValues.Add(EmptyItem);

            return cloneValues;
        }

        private void Combo_SelectedIndexChanged(object sender, EventArgs e)
        {
            AntdUI.Select combo = sender as AntdUI.Select;

            if (combo.Parent == null)
            {
                return;
            }

            int order = Convert.ToInt32(combo.Tag);

            if (order == combo.Parent.Controls.Count)
            {
                AntdUI.Select refCombo = this.CreateCombobox(this.panelReferenceTable, this.ReferenceTableColumns, null);

                this.panelReferenceTable.Controls.Add(refCombo);

                AntdUI.Select cbo = this.CreateCombobox(this.panelTable, this.TableColumns, null);

                this.panelTable.Controls.Add(cbo);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            List<ForeignKeyColumn> mappings = new List<ForeignKeyColumn>();

            for (int i = 0; i < this.panelReferenceTable.Controls.Count; i++)
            {
                AntdUI.Select refCombo = this.panelReferenceTable.Controls[i] as AntdUI.Select;
                AntdUI.Select combo = this.panelTable.Controls[i] as AntdUI.Select;

                if (!string.IsNullOrEmpty(refCombo.Text) && refCombo.Text != EmptyItem
                    && !string.IsNullOrEmpty(combo.Text) && combo.Text != EmptyItem)
                {
                    ForeignKeyColumn mapping = new ForeignKeyColumn();
                    mapping.ReferencedColumnName = refCombo.Text;
                    mapping.ColumnName = combo.Text;
                    mapping.Order = mappings.Count + 1;

                    mappings.Add(mapping);
                }
            }

            this.Mappings = mappings;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
