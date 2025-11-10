using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace bitirme_projesi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 1️⃣ Tüm ürünleri getir
        [HttpGet]
        public IActionResult GetAllProducts()
        {
            var products = _context.Products.ToList();
            return Ok(products);
        }

        // 🔹 2️⃣ Ürün ekle
        [HttpPost]
        public IActionResult AddProduct([FromBody] CreateProductDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Ürün verisi alınamadı." });

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description ?? "Açıklama yok",
                Price = dto.Price,
                ImageUrl = dto.ImageUrl ?? "images/default.png",
                CategoryId = dto.CategoryId,
                Stock = dto.Stock,
                Status = dto.Stock > 0 ? "Stokta var" : "Tükendi"
            };

            _context.Products.Add(product);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Ürün başarıyla eklendi.",
                product
            });
        }


        // 🔹 3️⃣ Kategoriye göre ürünleri getir
        [HttpGet("category/{categoryId}")]
        public IActionResult GetProductsByCategory(int categoryId)
        {
            var products = _context.Products
                .Where(p => p.CategoryId == categoryId)
                .ToList();

            if (products == null || !products.Any())
            {
                return NotFound(new { message = "Bu kategoriye ait ürün bulunamadı." });
            }

            return Ok(products);
        }

        // 🔹 4️⃣ Belirli ID'ye sahip ürünü getir
        [HttpGet("{id}")]
        public IActionResult GetProductById(int id)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            if (product.Category == null)
                product.Category = new Category { Name = "Belirtilmemiş" };

            return Ok(product);
        }

        // 🔹 5️⃣ Ürün güncelle
        [HttpPut("{id}")]
        public IActionResult UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            if (dto == null)
                return BadRequest(new { message = "Veri alınamadı." });

            var product = _context.Products.FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            // Alanları güncelle
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.ImageUrl = dto.ImageUrl;
            product.CategoryId = dto.CategoryId;
            product.Stock = dto.Stock;

            // Stoka göre durum
            product.Status = dto.Stock > 0 ? "Stokta var" : "Tükendi";

            _context.SaveChanges();

            return Ok(new
            {
                message = "Ürün başarıyla güncellendi.",
                product
            });
        }

        // 🔹 6️⃣ Ürün sil
        [HttpDelete("{id}")]
        public IActionResult DeleteProduct(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            _context.Products.Remove(product);
            _context.SaveChanges();

            return Ok(new { message = "Ürün başarıyla silindi." });
        }
    }
}
