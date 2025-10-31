using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

        //  ürünleri getir
        [HttpGet]
        public IActionResult GetAllProducts()
        {
            var products = _context.Products.ToList();
            return Ok(products);
        }

        //  Ürün ekle
        [HttpPost]
        public IActionResult AddProduct(Product product)
        {
            // 🔹 Eğer stok girilmediyse varsayılan 10 olsun
            if (product.Stock == 0)
                product.Stock = 10;

            // 🔹 Stok durumuna göre status ayarla
            product.Status = product.Stock > 0 ? "Stokta var" : "Tükendi";
            _context.Products.Add(product);
            _context.SaveChanges();
            return Ok(product);
        }

        //  Kategoriye göre ürünleri getir
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
        // 🔹 Belirli ID'ye sahip ürünü getir
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


    }
}
