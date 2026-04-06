using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal? OldPrice { get; set; }
        public string ImageUrl { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }

        // ⭐ Alt kategori alanı
        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        public int Stock { get; set; }      // stok adedi
        public string? Status { get; set; } // "Stokta var" / "Tükendi"

        // AI tarafinda yorumlardan uretilen ozet metni
        public string? AiSummary { get; set; }
        public DateTime? AiSummaryUpdatedAt { get; set; }

        // 🔹 Satıcı bilgisi (ürünü ekleyen kullanıcı)
        [ForeignKey("Seller")]
        public int? SellerId { get; set; }  // Nullable: Admin eklediyse null olabilir
        public User? Seller { get; set; }

        // 🔹 Onay durumu (Satıcı ürünleri için admin onayı gerekli)
        public bool IsApproved { get; set; } = true;  // Admin eklediyse true, Satıcı eklediyse false (onay bekliyor)
    }
}
