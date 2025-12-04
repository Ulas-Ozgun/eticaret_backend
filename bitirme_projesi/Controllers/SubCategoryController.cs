using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using Microsoft.EntityFrameworkCore;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubCategoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubCategoryController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 Tüm alt kategoriler
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.SubCategories.ToListAsync();
            return Ok(list);
        }

        // 🔹 Bir kategorinin alt kategorileri
        [HttpGet("ByCategory/{categoryId}")]
        public async Task<IActionResult> GetByCategory(int categoryId)
        {
            var list = await _context.SubCategories
                .Where(s => s.CategoryId == categoryId)
                .ToListAsync();

            return Ok(list);
        }
    }
}
