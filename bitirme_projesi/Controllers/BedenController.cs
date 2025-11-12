using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BedenController : ControllerBase
    {
        private readonly AppDbContext _context;
        public BedenController(AppDbContext context) => _context = context;

        // 🔹 Tüm bedenleri getir
        [HttpGet]
        public IActionResult GetAll()
        {
            var bedenler = _context.beden
                .Select(b => new { b.Id, b.BedenAdi })
                .ToList();
            return Ok(bedenler);
        }

        // 🔹 Belirli bir ürünün bedenlerini getir (urun_beden ilişkisi üzerinden)
        [HttpGet("product/{productId:int}")]
        public IActionResult GetByProduct(int productId)
        {
            var bedenler = _context.urun_beden
                .Include(ub => ub.Beden)
                .Where(ub => ub.ProductId == productId)
                .Select(ub => new { ub.Beden.Id, ub.Beden.BedenAdi })
                .ToList();

            return Ok(bedenler);
        }
        [HttpGet("byProduct/{productId}")]
        public IActionResult GetBedenByProduct(int productId)
        {
            var bedenler = _context.urun_beden
                .Where(ub => ub.ProductId == productId)
                .Include(ub => ub.Beden)
                .Select(ub => new
                {
                    Id = ub.Beden.Id,
                    BedenAdi = ub.Beden.BedenAdi
                })
                .ToList();

            return Ok(bedenler);
        }

    }
}
