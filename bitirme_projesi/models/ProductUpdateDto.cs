namespace bitirme_projesi.Models
{
    public class ProductUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? Stock { get; set; }
        public int? CategoryId { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<int>? BedenIds { get; set; }
        public List<int>? NumaraIds { get; set; }
    }

}
