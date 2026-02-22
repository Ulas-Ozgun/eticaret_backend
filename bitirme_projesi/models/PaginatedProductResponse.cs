namespace bitirme_projesi.Models
{
    public class PaginatedProductResponse
    {
        public List<object> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalBlocks { get; set; }
        public int CurrentBlock { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int BlockSize { get; set; }
        public bool HasMoreInBlock { get; set; }
        public bool HasNextBlock { get; set; }
    }
}
