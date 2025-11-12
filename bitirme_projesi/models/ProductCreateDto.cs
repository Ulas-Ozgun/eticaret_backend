using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace bitirme_projesi.Models

{
    public class ProductCreateDto
    {
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public decimal Price { get; set; }

        public int Stock { get; set; }

        public int CategoryId { get; set; }

        public IFormFile ImageFile { get; set; } // 🔹 Yüklenen dosya
    }
}
