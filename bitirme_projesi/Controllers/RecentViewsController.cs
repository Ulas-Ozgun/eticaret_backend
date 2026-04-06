using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecentViewsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RecentViewsController(AppDbContext context)
        {
            _context = context;
        }

        // 🔥 Ürün görüntülemesi kaydet — aynı ürün tekrar açılırsa eski kayıt silinir, en güncel tek kayıt en başa (en yeni ViewedAt) gelir
        [HttpPost]
        public IActionResult AddView([FromBody] RecentView view)
        {
            if (view.UserId <= 0 || view.ProductId <= 0)
                return BadRequest();

            var existing = _context.RecentViews.Where(x =>
                x.UserId == view.UserId && x.ProductId == view.ProductId);
            _context.RecentViews.RemoveRange(existing);

            view.Id = 0;
            view.ViewedAt = DateTime.UtcNow;
            _context.RecentViews.Add(view);
            _context.SaveChanges();

            return Ok(new { message = "Saved" });
        }

        // 🔥 Son görüntülenenler: ürün başına tek satır (en son bakılan), tarihe göre azalan
        [HttpGet("{userId}")]
        public IActionResult GetRecent(int userId)
        {
            const int limit = 11;

            var rows = _context.RecentViews
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ViewedAt)
                .Include(x => x.Product)
                .ToList();

            var seen = new HashSet<int>();
            var deduped = new List<RecentView>();
            foreach (var x in rows)
            {
                if (!seen.Add(x.ProductId))
                    continue;
                if (x.Product == null)
                    continue;
                deduped.Add(x);
                if (deduped.Count >= limit)
                    break;
            }

            var result = deduped.Select(x => new
            {
                x.Id,
                x.ProductId,
                x.ViewedAt,
                ProductName = x.Product!.Name,
                ImageUrl = x.Product.ImageUrl,
                Price = x.Product.Price
            }).ToList();

            return Ok(result);
        }
       

    }
}
