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

        // 🔥 Ürün görüntülemesi kaydet
        [HttpPost]
        public IActionResult AddView([FromBody] RecentView view)
        {
            view.ViewedAt = DateTime.UtcNow;

            _context.RecentViews.Add(view);
            _context.SaveChanges();

            return Ok(new { message = "Saved" });
        }

        // 🔥 Son 5 ürün
        [HttpGet("{userId}")]
        public IActionResult GetRecent(int userId)
        {
            var result = _context.RecentViews
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.ViewedAt)
                .Include(x => x.Product)
                .Take(11)
                .Select(x => new
                {
                    x.Id,
                    x.ProductId,
                    x.ViewedAt,
                    ProductName = x.Product.Name,
                    ImageUrl = x.Product.ImageUrl,
                    Price = x.Product.Price
                })
                .ToList();

            return Ok(result);
        }
       

    }
}
