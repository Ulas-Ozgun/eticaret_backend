using System.ComponentModel.DataAnnotations;

namespace bitirme_projesi.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }

        // ⭐ Alt kategori alanı
        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        public int Stock { get; set; }      // stok adedi
        public string? Status { get; set; } // "Stokta var" / "Tükendi"
    }
}
