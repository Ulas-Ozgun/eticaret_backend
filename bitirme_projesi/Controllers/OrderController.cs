using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 1️⃣ Kullanıcının siparişlerini getir
        [HttpGet("user/{userId}")]
        public IActionResult GetUserOrders(int userId)
        {
            var orders = _context.Orders
                .Include(o => o.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.Size,
                    o.Quantity,
                    o.TotalPrice,
                    o.OrderDate,
                    Product = new
                    {
                        o.Product.Name,
                        o.Product.ImageUrl,
                        o.Product.Price
                    }
                })
                .ToList();

            if (!orders.Any())
                return NotFound(new { message = "Henüz bir siparişiniz yok." });

            return Ok(orders);
        }
    }
}
