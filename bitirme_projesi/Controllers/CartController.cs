using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 1️⃣ Kullanıcının sepetini getir (Size dahil)
        [HttpGet("get/{userId}")]
        public IActionResult GetCart(int userId)
        {
            try
            {
                var cartItems = _context.Carts
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId)
                    .Select(c => new
                    {
                        c.Id,
                        c.Quantity,
                        c.Size, // 🔹 BEDEN/NUMARA
                        Product = new
                        {
                            c.Product.Id,
                            c.Product.Name,
                            c.Product.Description,
                            c.Product.Price,
                            c.Product.ImageUrl
                        },
                        Total = c.Product.Price * c.Quantity
                    })
                    .ToList();

                if (!cartItems.Any())
                    return NotFound(new { message = "Sepetiniz boş veya kullanıcı bulunamadı." });

                var totalCartAmount = cartItems.Sum(c => c.Total);

                return Ok(new
                {
                    message = "Sepet başarıyla getirildi.",
                    cartItems,
                    totalCartAmount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Sepet yüklenemedi.",
                    error = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }

        // 🔹 2️⃣ Sepete ürün ekle (SelectedSize eklendi)
        [HttpPost]
        public IActionResult AddToCart([FromBody] AddCartItemDto dto)
        {
            var user = _context.Users.Find(dto.UserId);
            var product = _context.Products.Find(dto.ProductId);

            if (user == null || product == null)
                return BadRequest(new { message = "Kullanıcı veya ürün bulunamadı." });

            // Aynı ürün ve aynı beden varsa miktar artır
            var existingItem = _context.Carts.FirstOrDefault(
                c => c.UserId == dto.UserId &&
                     c.ProductId == dto.ProductId &&
                     c.Size == dto.SelectedSize
            );

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
            }
            else
            {
                var cartItem = new Cart
                {
                    UserId = dto.UserId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    Size = dto.SelectedSize // 🔹 BEDEN/NUMARA BURADA KAYIT OLUYOR
                };

                _context.Carts.Add(cartItem);
            }

            _context.SaveChanges();
            return Ok(new { message = "Ürün sepete eklendi." });
        }

        // 🔹 3️⃣ Satın alma işlemi (stok düşürme)
        // 🔹 Satın alma işlemi (stok düşürme)
        [HttpPost("purchase/{userId}")]
        public IActionResult Purchase(int userId)
        {
            var cartItems = _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                .ToList();

            if (!cartItems.Any())
                return BadRequest(new { message = "Sepetiniz boş." });

            foreach (var item in cartItems)
            {
                var product = item.Product;

                if (product.Stock < item.Quantity)
                {
                    return BadRequest(new { message = $"{product.Name} ürünü için yeterli stok yok." });
                }

                // 🔹 Siparişi Orders tablosuna kaydet
                var order = new Order
                {
                    UserId = userId,
                    ProductId = product.Id,
                    Size = item.Size,
                    Quantity = item.Quantity,
                    TotalPrice = product.Price * item.Quantity
                };
                _context.Orders.Add(order);

                // 🔹 Stoktan düş
                product.Stock -= item.Quantity;
                if (product.Stock <= 0)
                    product.Status = "Tükendi";

                _context.Products.Update(product);
            }

            // 🔹 Sepeti temizle
            _context.Carts.RemoveRange(cartItems);
            _context.SaveChanges();

            return Ok(new { message = "Satın alma işlemi başarılı! 🎉" });
        }


        // 🔹 4️⃣ Sepet öğesini güncelle (miktar artır / azalt)
        [HttpPut("{id}")]
        public IActionResult UpdateCartItem(int id, [FromBody] AddCartItemDto dto)
        {
            try
            {
                var cartItem = _context.Carts
                    .Include(c => c.Product)
                    .FirstOrDefault(c => c.Id == id);

                if (cartItem == null)
                    return NotFound(new { message = "Ürün sepetinizde bulunamadı." });

                cartItem.Quantity = dto.Quantity;
                _context.Carts.Update(cartItem);
                _context.SaveChanges();

                return Ok(new
                {
                    message = "Sepet öğesi güncellendi.",
                    updatedItem = new
                    {
                        cartItem.Id,
                        cartItem.Quantity,
                        Product = new
                        {
                            cartItem.Product.Id,
                            cartItem.Product.Name,
                            cartItem.Product.Price
                        },
                        Total = cartItem.Product.Price * cartItem.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    message = "Güncelleme sırasında hata oluştu.",
                    error = ex.Message
                });
            }
        }

        // 🔹 5️⃣ Sepet öğesini sil
        [HttpDelete("{id}")]
        public IActionResult RemoveItem(int id)
        {
            try
            {
                var cartItem = _context.Carts.FirstOrDefault(c => c.Id == id);
                if (cartItem == null)
                    return NotFound("Ürün bulunamadı.");

                _context.Carts.Remove(cartItem);
                _context.SaveChanges();

                return Ok("Ürün sepetten silindi.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Silme işlemi başarısız.", error = ex.Message });
            }
        }

        // 🔹 6️⃣ Sepeti tamamen temizle
        [HttpDelete("clear/{userId}")]
        public IActionResult ClearCart(int userId)
        {
            try
            {
                var userCartItems = _context.Carts.Where(c => c.UserId == userId).ToList();

                if (!userCartItems.Any())
                    return NotFound(new { message = "Sepet zaten boş." });

                _context.Carts.RemoveRange(userCartItems);
                _context.SaveChanges();

                return Ok(new { message = "Sepet başarıyla temizlendi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Sepet temizlenemedi.", error = ex.Message });
            }
        }
    }
}
