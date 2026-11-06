using DatabaseManager.FileUtility.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseManager.FileUtility
{
    public class CsvReader : BaseReader
    {

        public CsvReader(SourceFileInfo info) : base(info) { }


        public override DataReadResult Read(bool onlyReadHeader = false)
        {
            DataReadResult result = new DataReadResult();           

            string filePath = this.info.FilePath;
            bool firstRowIsColumnName = this.info.FirstRowIsColumnName;

            var encoding = TextEncoding.Resolve(this.info.EncodingName);

            using (StreamReader textReader = encoding == null ? new StreamReader(filePath) : new StreamReader(filePath, encoding))
            {
                using var reader = CsvHelper.CsvReader.Create(textReader, new CsvHelper.CsvReaderOptions
                {
                    HasHeader = firstRowIsColumnName,
                    Separator = ',',
                    FormatProvider = System.Globalization.CultureInfo.CurrentCulture,
                    MalformedData = CsvHelper.CsvMalformedDataHandling.Throw,
                }, leaveOpen: true);

                result.HeaderColumns = firstRowIsColumnName ? reader.Headers.ToArray() : null;

                if(!onlyReadHeader)
                {
                    int index = 0;

                    Dictionary<int, Dictionary<int, object>> dict = new Dictionary<int, Dictionary<int, object>>();

                    while (reader.Read())
                    {
                        Dictionary<int, object> dictRow = new Dictionary<int, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string value = reader.GetString(i);

                            dictRow.Add(i, value);
                        }

                        dict.Add(index, dictRow);

                        index++;
                    }

                    result.Data = dict;
                }               
            }           

            return result;
        }
    }
}
