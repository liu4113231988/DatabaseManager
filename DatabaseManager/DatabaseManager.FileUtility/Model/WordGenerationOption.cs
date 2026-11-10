namespace DatabaseManager.FileUtility.Model
{
    public class WordGenerationOption
    {
        public string FilePath { get; set; } = string.Empty;
        public string? GridColumnHeaderBackgroundColor { get; set; }
        public string? GridColumnHeaderForegroundColor { get; set; }
        public bool GridColumnHeaderFontIsBold { get; set; }  
    }
}
