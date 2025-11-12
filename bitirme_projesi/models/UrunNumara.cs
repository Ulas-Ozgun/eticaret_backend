using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.Models
{
    [Table("urun_numara")]
    public class UrunNumara
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("numara_id")]
        public int NumaraId { get; set; }

        // 🔹 Navigation Properties
        public Product Product { get; set; }
        public Numara Numara { get; set; }
    }
}
