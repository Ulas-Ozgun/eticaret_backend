using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.models
{
    public class UrunNumara
    {
        public int Id { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [ForeignKey("Numara")]
        public int NumaraId { get; set; }
        public Numara Numara { get; set; }
    }

}
