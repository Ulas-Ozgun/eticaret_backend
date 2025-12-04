using System.Collections.Generic;

namespace bitirme_projesi.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        // Ana kategori ile ilişki
        public int CategoryId { get; set; }
        public Category Category { get; set; }

        public string Name { get; set; }

        // Bu alt kategoriye bağlı ürünler (isteğe bağlı ama güzel durur)
        public ICollection<Product>? Products { get; set; }
    }
}
