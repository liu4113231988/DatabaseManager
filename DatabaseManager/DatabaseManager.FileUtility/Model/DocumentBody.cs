using System.Collections.Generic;

namespace DatabaseManager.FileUtility.Model
{ 
    public class DocumentBody
    {
        public List<DocumentPart> Parts { get; set; } = new List<DocumentPart>();
    }

    public class DocumentPart
    {
        public string Title { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public GridData Data { get; set; } = new GridData();
    }
}
