using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.Models
{
    [Table("beden")]
    public class Beden
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("beden_adi")]
        public string BedenAdi { get; set; }

        // 🔹 İlişki (1 beden birçok urun_beden ilişkisine sahip olabilir)
        public ICollection<UrunBeden> UrunBedenler { get; set; }
    }
}
