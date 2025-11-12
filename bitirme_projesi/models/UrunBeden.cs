using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.Models
{
    [Table("urun_beden")]
    public class UrunBeden
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("beden_id")]
        public int BedenId { get; set; }

        // 🔹 Navigation Properties
        public Product Product { get; set; }
        public Beden Beden { get; set; }
    }
}
