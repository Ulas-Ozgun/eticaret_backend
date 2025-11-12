using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.Models
{
    [Table("numara")]
    public class Numara
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("numara")]
        public int NumaraDegeri { get; set; }

        // 🔹 İlişki
        public ICollection<UrunNumara> UrunNumaralar { get; set; }
    }
}
