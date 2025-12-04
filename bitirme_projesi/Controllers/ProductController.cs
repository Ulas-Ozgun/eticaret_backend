using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models; // ✅ Bunu ekle
using System.Linq;
using System.IO;


namespace bitirme_projesi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 🔹 1️⃣ Tüm ürünleri getir
        [HttpGet]
        public IActionResult GetAllProducts()
        {
            var products = _context.Products.Include(p => p.Category).ToList();
            return Ok(products);
        }

        // 🔹 2️⃣ Tek ürün getir
        [HttpGet("{id}")]
        public IActionResult GetProductById(int id)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            return Ok(product);
        }

        // 🔹 3️⃣ Yeni ürün ekle (FormData ile)
        [HttpPost("add-with-image")]
        public async Task<IActionResult> AddProductWithImage([FromForm] ProductCreateDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = new Product
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                Stock = model.Stock,
                CategoryId = model.CategoryId,
                Status = model.Stock > 0 ? "Stokta var" : "Tükendi"
            };

            // 🔹 Resim yükleme
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                product.ImageUrl = $"images/{uniqueFileName}";
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // 🔹 Beden ekleme
            if (model.BedenIds != null && model.BedenIds.Any())
            {
                foreach (var bedenId in model.BedenIds)
                {
                    _context.urun_beden.Add(new UrunBeden
                    {
                        ProductId = product.Id,
                        BedenId = bedenId
                    });
                }
            }

            // 🔹 Numara ekleme
            if (model.NumaraIds != null && model.NumaraIds.Any())
            {
                foreach (var numaraId in model.NumaraIds)
                {
                    _context.urun_numara.Add(new UrunNumara
                    {
                        ProductId = product.Id,
                        NumaraId = numaraId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Ürün başarıyla eklendi!", product });
        }

        // 🔹 4️⃣ Ürün güncelle (FormData ile)

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductUpdateDto model)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

            // Alanlar geldiyse güncelle, gelmediyse eskisini koru
            if (model.Name != null) product.Name = model.Name;
            if (model.Description != null) product.Description = model.Description;
            if (model.Price.HasValue) product.Price = model.Price.Value;
            if (model.Stock.HasValue) product.Stock = model.Stock.Value;
            if (model.CategoryId.HasValue) product.CategoryId = model.CategoryId.Value;

            product.Status = product.Stock > 0 ? "Stokta var" : "Tükendi";

            // Yeni resim geldiyse değiştir
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageFile.CopyToAsync(stream);

                product.ImageUrl = $"images/{uniqueFileName}";
            }

            // İlişkileri güncelle (gönderildiyse)
            if (model.BedenIds != null)
            {
                var eskiBedenler = _context.urun_beden.Where(x => x.ProductId == id);
                _context.urun_beden.RemoveRange(eskiBedenler);
                foreach (var bedenId in model.BedenIds)
                    _context.urun_beden.Add(new UrunBeden { ProductId = id, BedenId = bedenId });
            }

            if (model.NumaraIds != null)
            {
                var eskiNumaralar = _context.urun_numara.Where(x => x.ProductId == id);
                _context.urun_numara.RemoveRange(eskiNumaralar);
                foreach (var numaraId in model.NumaraIds)
                    _context.urun_numara.Add(new UrunNumara { ProductId = id, NumaraId = numaraId });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "✏️ Ürün başarıyla güncellendi!", product });
        }



        // 🔹 5️⃣ Ürün sil
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            _context.Products.Remove(product);

            var urunBedenler = _context.urun_beden.Where(x => x.ProductId == id);
            var urunNumaralar = _context.urun_numara.Where(x => x.ProductId == id);
            _context.urun_beden.RemoveRange(urunBedenler);
            _context.urun_numara.RemoveRange(urunNumaralar);

            await _context.SaveChangesAsync();

            return Ok(new { message = "🗑️ Ürün başarıyla silindi!" });
        }
        [HttpGet("slider")]
        public IActionResult GetSliderImages()
        {
            var folder = Path.Combine(_env.WebRootPath, "slider-images");

            if (!Directory.Exists(folder))
                return Ok(new List<string>());

            var files = Directory.GetFiles(folder)
                .Select(f => "slider-images/" + Path.GetFileName(f))
                .ToList();

            return Ok(files);
        }


    }
}
