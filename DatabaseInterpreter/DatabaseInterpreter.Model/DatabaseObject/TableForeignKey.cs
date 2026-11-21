using System.Collections.Generic;
using System.Linq;

namespace DatabaseInterpreter.Model
{
    public class TableForeignKey:TableChild
    {
        public string ReferencedSchema { get; set; }
        public string ReferencedTableName { get; set; }
        public bool UpdateCascade { get; set; }
        public bool DeleteCascade { get; set; }

        public List<ForeignKeyColumn> Columns { get; set; } = new List<ForeignKeyColumn>();

        /// <summary>无反射深拷贝，供 native AOT 元数据克隆使用。</summary>
        public TableForeignKey DeepClone()
        {
            var clone = (TableForeignKey)MemberwiseClone();
            clone.Columns = Columns.Select(c => c.Clone()).ToList();
            return clone;
        }
    }

    public class ForeignKeyColumn: SimpleColumn
    {      
        public string ReferencedColumnName { get; set; }

        public ForeignKeyColumn Clone() => (ForeignKeyColumn)MemberwiseClone();
    }

    public class TableForeignKeyItem: TableColumnChild
    {
        public string ReferencedSchema { get; set; }
        public string ReferencedTableName { get; set; }
        public string ReferencedColumnName { get; set; }
        public bool UpdateCascade { get; set; }
        public bool DeleteCascade { get; set; }

        public string TableFullName => string.IsNullOrEmpty(this.Schema)? this.TableName:  this.Schema + "." + this.TableName;
        public string ReferencedTableFullName => string.IsNullOrEmpty(this.Schema) ? this.ReferencedTableName :  this.Schema + "." + this.ReferencedTableName;
    }
}
