namespace MetromontCastLink.Shared.Models
{
    public class MenuItem
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string? ParentId { get; set; }
        public string? IconCss { get; set; }
        public string? NavigateUrl { get; set; }
        public bool HasChild { get; set; }
        public bool Expanded { get; set; }
    }
}