using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NumaraController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NumaraController(AppDbContext context) => _context = context;

        // 🔹 Tüm numaraları getir
        [HttpGet]
        public IActionResult GetAll()
        {
            var numaralar = _context.numara
                .Select(n => new { n.Id, n.NumaraDegeri })
                .ToList();
            return Ok(numaralar);
        }

        // 🔹 Belirli bir ürünün numaralarını getir (urun_numara ilişkisi üzerinden)
        [HttpGet("product/{productId:int}")]
        public IActionResult GetByProduct(int productId)
        {
            var numaralar = _context.urun_numara
                .Include(un => un.Numara)
                .Where(un => un.ProductId == productId)
                .Select(un => new { un.Numara.Id, un.Numara.NumaraDegeri })
                .ToList();

            return Ok(numaralar);
        }
        [HttpGet("byProduct/{productId}")]
        public IActionResult GetNumaraByProduct(int productId)
        {
            var numaralar = _context.urun_numara
                .Where(un => un.ProductId == productId)
                .Include(un => un.Numara)
                .Select(un => new
                {
                    Id = un.Numara.Id,
                    NumaraDegeri = un.Numara.NumaraDegeri
                })
                .ToList();

            return Ok(numaralar);
        }

    }
}
