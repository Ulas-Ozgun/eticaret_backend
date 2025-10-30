using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
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
    }
}
