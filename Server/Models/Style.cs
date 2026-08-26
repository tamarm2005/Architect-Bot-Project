namespace Models
{
    public class Style
    {
        public int Id { get; set; }
        public DesignStyle Category { get; set; }
        public string StyleName { get; set; } = string.Empty;
        public string ColorPalette { get; set; } = string.Empty;
        public string Materials { get; set; } = string.Empty;
    }
}

