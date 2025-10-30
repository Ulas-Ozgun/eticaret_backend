using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductSeederController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductSeederController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("generate/{count}")]
        public IActionResult GenerateProducts(int count)
        {
            var random = new Random();

            
            if (!_context.Categories.Any())
            {
                var defaultCategories = new List<Category>
                {
                    new Category { Name = "Giyim" },
                    new Category { Name = "Elektronik" },
                    new Category { Name = "Ev & Yaşam" },
                    new Category { Name = "Ayakkabı" },
                    new Category { Name = "Saat & Aksesuar" }
                };
                _context.Categories.AddRange(defaultCategories);
                _context.SaveChanges();
            }

            var categoryIds = _context.Categories.Select(c => c.Id).ToList();
            var products = new List<Product>();

           
            var imageUrls = new Dictionary<string, string>
            {
                { "Giyim", "https://picsum.photos/seed/giyim-" },
                { "Elektronik", "https://picsum.photos/seed/elektronik-" },
                { "Ev & Yaşam", "https://picsum.photos/seed/evyasam-" },
                { "Ayakkabı", "https://picsum.photos/seed/ayakkabi-" },
                { "Saat & Aksesuar", "https://picsum.photos/seed/saataksesuar-" }
            };

            for (int i = 1; i <= count; i++)
            {
                var categoryId = categoryIds[random.Next(categoryIds.Count)];
                var category = _context.Categories.First(c => c.Id == categoryId);

                // her ürüne tamamen benzersiz resim
                var imageUrl = $"{imageUrls[category.Name]}{Guid.NewGuid()}/400/300";

                products.Add(new Product
                {
                    Name = $"Ürün {i}",
                    Description = $"Bu ürün {category.Name} kategorisindedir.",
                    Price = (decimal)Math.Round(random.NextDouble() * 900 + 100, 2),
                    CategoryId = categoryId,
                    ImageUrl = imageUrl
                });
            }

            _context.Products.AddRange(products);
            _context.SaveChanges();

            return Ok($"{count} ürün rastgele kategorilerle ve görsellerle eklendi ✅");
        }
    }
}
