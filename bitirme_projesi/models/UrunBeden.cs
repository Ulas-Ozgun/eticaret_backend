using System.ComponentModel.DataAnnotations.Schema;

namespace bitirme_projesi.models
{
    public class UrunBeden
    {
        public int Id { get; set; }

        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public Product Product { get; set; }

        [ForeignKey("Beden")]
        public int BedenId { get; set; }
        public Beden Beden { get; set; }
    }
}