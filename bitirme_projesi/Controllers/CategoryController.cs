using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 Tüm kategorileri getir
        [HttpGet]
        public IActionResult GetAllCategories()
        {
            var categories = _context.Categories.ToList();
            return Ok(categories);
        }

        // 🔹 Yeni kategori ekle 
        [HttpPost]
        public IActionResult AddCategory(Category category)
        {
            _context.Categories.Add(category);
            _context.SaveChanges();
            return Ok(category);
        }
    }
}
