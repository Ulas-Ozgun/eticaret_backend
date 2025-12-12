using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using System.Text.Json;

namespace bitirme_projesi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SeedController(AppDbContext context, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _env = env;
            _configuration = configuration;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }

        /// <summary>
        /// 🔥 TAM SEED - Her kategoriye uygun ürünler ekler (Dinamik resimlerle)
        /// GET: api/Seed/full-dynamic?countPerCategory=200
        /// Tüm resimler Unsplash'tan dinamik olarak çekilir (kategoriye uygun)
        /// </summary>
        [HttpGet("full-dynamic")]
        public async Task<IActionResult> FullSeedDynamic([FromQuery] int countPerCategory = 150)
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();

                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                var random = new Random();
                var productsToAdd = new List<Product>();
                var categoryStats = new Dictionary<string, int>();

                foreach (var category in categories)
                {
                    var catName = category.Name?.ToLower() ?? "";
                    var productTemplates = GetCategoryProducts(catName);

                    // Tüm şablonlardaki resimleri topla (daha fazla çeşitlilik için)
                    var allCategoryImages = new List<string>();
                    foreach (var template in productTemplates)
                    {
                        if (template.Images != null && template.Images.Length > 0)
                        {
                            allCategoryImages.AddRange(template.Images);
                        }
                    }
                    
                    // Her ürün için benzersiz resim seçimi için kullanılan resimleri takip et
                    var usedImageUrls = new HashSet<string>();
                    var imageCounter = 0; // Her ürün için farklı bir sayaç
                    
                    for (int i = 0; i < countPerCategory; i++)
                    {
                        var template = productTemplates[random.Next(productTemplates.Count)];

                        var price = Math.Round((decimal)(template.MinPrice + random.NextDouble() * (template.MaxPrice - template.MinPrice)), 2);
                        var stock = random.Next(5, 300);
                        var productName = AddVariation(template.Name, i, random);

                        // 🔥 Şablonlardaki kategoriye uygun resimleri kullan
                        // Tüm kategori resimlerinden her ürün için farklı bir resim seç
                        string imageUrl = null;
                        
                        if (allCategoryImages.Any())
                        {
                            // Her ürün için benzersiz bir index ile resim seç
                            var uniqueImageIndex = (i * 7919 + random.Next(10000) + imageCounter * 17 + (int)(DateTime.Now.Ticks % 1000)) % allCategoryImages.Count;
                            imageUrl = allCategoryImages[uniqueImageIndex];
                            
                            // Eğer bu resim daha önce kullanıldıysa, farklı bir resim seç
                            var attempts = 0;
                            while (usedImageUrls.Contains(imageUrl) && attempts < allCategoryImages.Count)
                            {
                                uniqueImageIndex = (uniqueImageIndex + 1 + attempts * 7919) % allCategoryImages.Count;
                                imageUrl = allCategoryImages[uniqueImageIndex];
                                attempts++;
                            }
                            
                            // Eğer hala aynı resimse, benzersiz parametre ekle
                            if (usedImageUrls.Contains(imageUrl))
                            {
                                var uniqueSeed = (i * 7919L) + (random.Next(1000000) * 17L) + (catName.GetHashCode() * 31L) + (DateTime.Now.Ticks % 1000000) + imageCounter;
                                var separator = imageUrl.Contains("?") ? "&" : "?";
                                imageUrl = $"{imageUrl}{separator}v={uniqueSeed}";
                            }
                            
                            usedImageUrls.Add(imageUrl);
                            imageCounter++;
                        }
                        
                        // Eğer hala resim yoksa, şablonlardaki diğer ürünlerden bir resim al
                        if (string.IsNullOrEmpty(imageUrl) && template.Images != null && template.Images.Length > 0)
                        {
                            imageUrl = template.Images[random.Next(template.Images.Length)];
                        }

                        var product = new Product
                        {
                            Name = productName,
                            Description = template.Description,
                            Price = price,
                            Stock = stock,
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            ImageUrl = imageUrl,
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[category.Name ?? "Bilinmeyen"] = countPerCategory;
                }

                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ Toplam {productsToAdd.Count} ürün eklendi! (Tüm resimler dinamik Unsplash'tan)",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    imageSource = "Unsplash Dynamic API"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// 🔥 TAM SEED - Her kategoriye uygun ürünler ekler
        /// GET: api/Seed/full?countPerCategory=200
        /// </summary>
        [HttpGet("full")]
        public async Task<IActionResult> FullSeed([FromQuery] int countPerCategory = 150)
        {
            try
            {
                // 🔹 1. Önce alt kategorileri oluştur/güncelle
                await EnsureSubCategoriesExist();

                var categories = await _context.Categories.ToListAsync();
                var subCategories = await _context.SubCategories.ToListAsync();

                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                var random = new Random();
                var productsToAdd = new List<Product>();
                var categoryStats = new Dictionary<string, int>();

                foreach (var category in categories)
                {
                    var catName = category.Name?.ToLower() ?? "";
                    var relevantSubs = subCategories.Where(s => s.CategoryId == category.Id).ToList();
                    var productTemplates = GetCategoryProducts(catName);

                    for (int i = 0; i < countPerCategory; i++)
                    {
                        var template = productTemplates[random.Next(productTemplates.Count)];

                        var price = Math.Round((decimal)(template.MinPrice + random.NextDouble() * (template.MaxPrice - template.MinPrice)), 2);
                        var stock = random.Next(5, 300);

                        // Ürün adına varyasyon ekle
                        var productName = AddVariation(template.Name, i, random);

                        // 🔥 Resim seçimi: %70 şablondan, %30 dinamik Unsplash'tan
                        string imageUrl;
                        if (random.Next(10) < 7 && template.Images.Length > 0)
                        {
                            // Şablondaki resimlerden seç
                            imageUrl = template.GetRandomImage(random);
                        }
                        else
                        {
                            // Kategoriye göre dinamik Unsplash resmi
                            imageUrl = GetDynamicUnsplashImage(catName, i, random, productTemplates);
                        }

                        var product = new Product
                        {
                            Name = productName,
                            Description = template.Description,
                            Price = price,
                            Stock = stock,
                            CategoryId = category.Id,
                            SubCategoryId = null,  // 🔥 Alt kategori yok, sadece ana kategori
                            ImageUrl = imageUrl,
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[category.Name ?? "Bilinmeyen"] = countPerCategory;
                }

                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ Toplam {productsToAdd.Count} ürün eklendi!",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Alt kategorileri oluştur/güncelle
        /// </summary>
        private async Task EnsureSubCategoriesExist()
        {
            var categories = await _context.Categories.ToListAsync();
            var existingSubs = await _context.SubCategories.ToListAsync();

            var subCategoryData = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Elektronik", new List<string> { "Telefon", "Laptop", "Tablet", "Kulaklık", "Akıllı Saat", "Kamera", "Televizyon", "Hoparlör" } },
                { "Giyim", new List<string> { "T-Shirt", "Gömlek", "Pantolon", "Elbise", "Ceket", "Kazak", "Mont", "Sweatshirt" } },
                { "Ayakkabı", new List<string> { "Spor Ayakkabı", "Günlük Ayakkabı", "Bot", "Topuklu", "Sandalet", "Sneaker" } },
                { "Kozmetik", new List<string> { "Parfüm", "Ruj", "Fondöten", "Maskara", "Cilt Bakım", "Saç Bakım", "Makyaj Seti" } },
                { "Çanta", new List<string> { "El Çantası", "Sırt Çantası", "Omuz Çantası", "Cüzdan", "Bavul", "Laptop Çantası" } },
                { "Kitap", new List<string> { "Roman", "Kişisel Gelişim", "Tarih", "Bilim", "Çocuk Kitabı", "Edebiyat", "Felsefe" } }
            };

            foreach (var category in categories)
            {
                if (category.Name == null) continue;
                
                if (subCategoryData.TryGetValue(category.Name, out var subNames))
                {
                    foreach (var subName in subNames)
                    {
                        if (!existingSubs.Any(s => s.CategoryId == category.Id && s.Name == subName))
                        {
                            _context.SubCategories.Add(new SubCategory
                            {
                                Name = subName,
                                CategoryId = category.Id
                            });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Kategoriye göre ürün şablonları döndürür
        /// </summary>
        private List<ProductTemplate> GetCategoryProducts(string categoryName)
        {
            return categoryName switch
            {
                var c when c.Contains("elektronik") => GetElektronikProducts(),
                var c when c.Contains("giyim") => GetGiyimProducts(),
                var c when c.Contains("ayakkab") => GetAyakkabiProducts(),
                var c when c.Contains("kozmetik") => GetKozmetikProducts(),
                var c when c.Contains("çanta") || c.Contains("canta") => GetCantaProducts(),
                var c when c.Contains("kitap") => GetKitapProducts(),
                _ => GetElektronikProducts()
            };
        }

        private List<ProductTemplate> GetElektronikProducts() => new()
        {
            // Telefonlar
            new("iPhone 15 Pro Max", "Apple'ın en yeni amiral gemisi telefon. A17 Pro çip, 48MP kamera.", 45000, 75000, 
                new[] { "https://images.unsplash.com/photo-1592750475338-74b7b21085ab?w=400", "https://images.unsplash.com/photo-1510557880182-3d4d3cba35a5?w=400" }, "Telefon"),
            new("Samsung Galaxy S24 Ultra", "200MP kamera, S Pen desteği, Snapdragon 8 Gen 3.", 42000, 65000,
                new[] { "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?w=400", "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=400" }, "Telefon"),
            new("Xiaomi 14 Pro", "Leica kamera, Snapdragon 8 Gen 3, 120W hızlı şarj.", 28000, 42000,
                new[] { "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=400" }, "Telefon"),
            // Laptoplar
            new("MacBook Pro M3", "Apple M3 çip, 16GB RAM, 512GB SSD, 14 inç Retina ekran.", 55000, 95000,
                new[] { "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=400", "https://images.unsplash.com/photo-1541807084-5c52b6b3adef?w=400" }, "Laptop"),
            new("Dell XPS 15", "Intel Core i7, 16GB RAM, 512GB SSD, OLED ekran.", 35000, 55000,
                new[] { "https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?w=400", "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?w=400" }, "Laptop"),
            new("ASUS ROG Gaming Laptop", "Intel i9, RTX 4080, 32GB RAM, 1TB SSD.", 65000, 95000,
                new[] { "https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?w=400" }, "Laptop"),
            // Tabletler
            new("iPad Pro 12.9", "M2 çip, Liquid Retina XDR ekran, 256GB.", 32000, 48000,
                new[] { "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=400", "https://images.unsplash.com/photo-1585790050230-5dd28404ccb9?w=400" }, "Tablet"),
            new("Samsung Galaxy Tab S9", "Snapdragon 8 Gen 2, 12.4 inç AMOLED, S Pen.", 25000, 38000,
                new[] { "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=400" }, "Tablet"),
            // Kulaklıklar
            new("Sony WH-1000XM5", "Kablosuz kulaklık, aktif gürültü engelleme, 30 saat pil.", 8000, 12000,
                new[] { "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=400", "https://images.unsplash.com/photo-1583394838336-acd977736f90?w=400" }, "Kulaklık"),
            new("AirPods Pro 2", "Aktif gürültü engelleme, MagSafe şarj kutusu.", 6000, 9000,
                new[] { "https://images.unsplash.com/photo-1600294037681-c80b4cb5b434?w=400", "https://images.unsplash.com/photo-1588423771073-b8903fbb85b5?w=400" }, "Kulaklık"),
            new("JBL Tune 760NC", "Kablosuz kulaklık, ANC, 35 saat pil ömrü.", 2500, 4000,
                new[] { "https://images.unsplash.com/photo-1583394838336-acd977736f90?w=400" }, "Kulaklık"),
            // Akıllı Saatler
            new("Apple Watch Ultra 2", "Titanyum kasa, çift frekanslı GPS, 36 saat pil.", 28000, 38000,
                new[] { "https://images.unsplash.com/photo-1546868871-7041f2a55e12?w=400", "https://images.unsplash.com/photo-1579586337278-3befd40fd17a?w=400" }, "Akıllı Saat"),
            new("Samsung Galaxy Watch 6", "BioActive sensör, Wear OS, Super AMOLED.", 8000, 14000,
                new[] { "https://images.unsplash.com/photo-1579586337278-3befd40fd17a?w=400" }, "Akıllı Saat"),
            // TV
            new("Samsung 65\" QLED TV", "4K UHD, Quantum HDR, Smart TV, 120Hz.", 25000, 45000,
                new[] { "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?w=400", "https://images.unsplash.com/photo-1461151304267-38535e780c79?w=400" }, "Televizyon"),
            new("LG OLED 55\" C3", "4K OLED, Dolby Vision, 120Hz, webOS.", 35000, 55000,
                new[] { "https://images.unsplash.com/photo-1461151304267-38535e780c79?w=400" }, "Televizyon"),
            // Hoparlör
            new("JBL Charge 5", "Taşınabilir Bluetooth hoparlör, 20 saat pil, su geçirmez.", 2500, 4500,
                new[] { "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=400", "https://images.unsplash.com/photo-1545454675-3531b543be5d?w=400" }, "Hoparlör"),
            new("Sonos One", "Akıllı hoparlör, Alexa ve Google Assistant desteği.", 5000, 8000,
                new[] { "https://images.unsplash.com/photo-1545454675-3531b543be5d?w=400" }, "Hoparlör"),
            // Kamera
            new("Canon EOS R6", "Aynasız fotoğraf makinesi, 20MP, 4K video, IBIS.", 45000, 65000,
                new[] { "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=400", "https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=400" }, "Kamera"),
            new("Sony A7 IV", "Full frame aynasız, 33MP, 4K 60fps video.", 55000, 75000,
                new[] { "https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=400" }, "Kamera"),
        };

        private List<ProductTemplate> GetGiyimProducts() => new()
        {
            // T-Shirt
            new("Nike Dri-FIT T-Shirt", "Nefes alabilen kumaş, spor için ideal, nem çekici.", 400, 900,
                new[] { "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=400", "https://images.unsplash.com/photo-1583743814966-8936f5b7be1a?w=400" }, "T-Shirt"),
            new("Adidas Essential T-Shirt", "Pamuklu, rahat kesim, klasik logo.", 350, 700,
                new[] { "https://images.unsplash.com/photo-1583743814966-8936f5b7be1a?w=400" }, "T-Shirt"),
            new("Puma Basic Tee", "Yumuşak pamuk, günlük kullanım, çeşitli renkler.", 250, 500,
                new[] { "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=400" }, "T-Shirt"),
            // Gömlek
            new("H&M Slim Fit Gömlek", "Pamuklu kumaş, düğmeli yaka, şık tasarım.", 500, 1200,
                new[] { "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?w=400", "https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?w=400" }, "Gömlek"),
            new("Zara Oxford Gömlek", "Klasik kesim, ofis stili, %100 pamuk.", 700, 1500,
                new[] { "https://images.unsplash.com/photo-1602810318383-e386cc2a3ccf?w=400" }, "Gömlek"),
            new("Tommy Hilfiger Gömlek", "Premium kumaş, işlemeli logo, slim fit.", 1200, 2500,
                new[] { "https://images.unsplash.com/photo-1596755094514-f87e34085b2c?w=400" }, "Gömlek"),
            // Pantolon
            new("Levi's 501 Original Jean", "Klasik kesim, %100 pamuk, zamansız tasarım.", 1200, 2500,
                new[] { "https://images.unsplash.com/photo-1542272604-787c3835535d?w=400", "https://images.unsplash.com/photo-1541099649105-f69ad21f3246?w=400" }, "Pantolon"),
            new("Pull&Bear Jogger Pantolon", "Rahat kesim, elastik bel, günlük kullanım.", 400, 900,
                new[] { "https://images.unsplash.com/photo-1624378439575-d8705ad7ae80?w=400", "https://images.unsplash.com/photo-1552902865-b72c031ac5ea?w=400" }, "Pantolon"),
            new("Mavi Jeans Skinny", "Dar kesim, streç denim, modern stil.", 800, 1600,
                new[] { "https://images.unsplash.com/photo-1541099649105-f69ad21f3246?w=400" }, "Pantolon"),
            // Elbise
            new("Mango Midi Elbise", "Çiçek desenli, yazlık, rahat kesim.", 800, 1800,
                new[] { "https://images.unsplash.com/photo-1595777457583-95e059d581b8?w=400", "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?w=400" }, "Elbise"),
            new("Zara Saten Elbise", "Zarif saten kumaş, akşam yemeği için ideal.", 1500, 3000,
                new[] { "https://images.unsplash.com/photo-1572804013309-59a88b7e92f1?w=400" }, "Elbise"),
            new("H&M Yaz Elbisesi", "Hafif kumaş, çiçek deseni, rahat kesim.", 500, 1000,
                new[] { "https://images.unsplash.com/photo-1595777457583-95e059d581b8?w=400" }, "Elbise"),
            // Ceket
            new("Zara Oversize Blazer", "Şık ve modern kesim, ofis ve günlük kullanım.", 1500, 3500,
                new[] { "https://images.unsplash.com/photo-1594938298603-c8148c4dae35?w=400", "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=400" }, "Ceket"),
            new("Mango Deri Ceket", "Suni deri, biker stil, fermuar detaylar.", 2000, 4000,
                new[] { "https://images.unsplash.com/photo-1551028719-00167b16eac5?w=400" }, "Ceket"),
            // Kazak
            new("Calvin Klein Kazak", "Yün karışımlı, V yaka, minimalist tasarım.", 1200, 2800,
                new[] { "https://images.unsplash.com/photo-1434389677669-e08b4cac3105?w=400", "https://images.unsplash.com/photo-1576566588028-4147f3842f27?w=400" }, "Kazak"),
            new("Koton Triko Kazak", "Örgü desen, sıcak tutan, kışlık.", 400, 900,
                new[] { "https://images.unsplash.com/photo-1576566588028-4147f3842f27?w=400" }, "Kazak"),
            // Mont
            new("The North Face Mont", "Su geçirmez, rüzgar geçirmez, kışlık.", 3500, 7000,
                new[] { "https://images.unsplash.com/photo-1544923246-77307dd628b5?w=400", "https://images.unsplash.com/photo-1591047139829-d91aecb6caea?w=400" }, "Mont"),
            new("Columbia Kışlık Mont", "Omni-Heat teknolojisi, su geçirmez.", 2500, 5000,
                new[] { "https://images.unsplash.com/photo-1591047139829-d91aecb6caea?w=400" }, "Mont"),
            // Sweatshirt
            new("Adidas Originals Sweatshirt", "Pamuklu, kapüşonlu, klasik üç çizgi.", 900, 1800,
                new[] { "https://images.unsplash.com/photo-1556821840-3a63f95609a7?w=400", "https://images.unsplash.com/photo-1578681994506-b8f463449011?w=400" }, "Sweatshirt"),
            new("Nike Club Hoodie", "Fleece kumaş, kanguru cep, kapüşonlu.", 800, 1500,
                new[] { "https://images.unsplash.com/photo-1578681994506-b8f463449011?w=400" }, "Sweatshirt"),
        };

        private List<ProductTemplate> GetAyakkabiProducts() => new()
        {
            // Spor Ayakkabı
            new("Nike Air Max 270", "Air yastıklama, hafif taban, spor ve günlük.", 2500, 4500,
                new[] { "https://images.unsplash.com/photo-1542291026-7eec264c27ff?w=400", "https://images.unsplash.com/photo-1460353581641-37baddab0fa2?w=400" }, "Spor Ayakkabı"),
            new("Adidas Ultraboost 23", "Boost teknolojisi, Primeknit üst, koşu ayakkabısı.", 3500, 5500,
                new[] { "https://images.unsplash.com/photo-1556906781-9a412961c28c?w=400", "https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?w=400" }, "Spor Ayakkabı"),
            new("Puma RS-X", "Retro koşu stili, kalın taban, dikkat çekici tasarım.", 2000, 3500,
                new[] { "https://images.unsplash.com/photo-1608231387042-66d1773070a5?w=400", "https://images.unsplash.com/photo-1600185365483-26d7a4cc7519?w=400" }, "Spor Ayakkabı"),
            new("Under Armour HOVR", "Enerji geri dönüşü, nefes alabilen üst.", 2200, 4000,
                new[] { "https://images.unsplash.com/photo-1595950653106-6c9ebd614d3a?w=400" }, "Spor Ayakkabı"),
            // Günlük Ayakkabı
            new("New Balance 574", "Retro tasarım, ENCAP yastıklama, günlük kullanım.", 2000, 3500,
                new[] { "https://images.unsplash.com/photo-1539185441755-769473a23570?w=400", "https://images.unsplash.com/photo-1551107696-a4b0c5a0d9a2?w=400" }, "Günlük Ayakkabı"),
            new("Skechers Go Walk", "Memory foam tabanlık, ultra hafif, yürüyüş.", 1500, 2500,
                new[] { "https://images.unsplash.com/photo-1562183241-b937e95585b6?w=400", "https://images.unsplash.com/photo-1603808033192-082d6919d3e1?w=400" }, "Günlük Ayakkabı"),
            new("Reebok Classic Leather", "Retro tasarım, yumuşak deri, günlük.", 1500, 2800,
                new[] { "https://images.unsplash.com/photo-1605348532760-6753d2c43329?w=400", "https://images.unsplash.com/photo-1584735175315-9d5df23860e6?w=400" }, "Günlük Ayakkabı"),
            // Bot
            new("Timberland 6-Inch Boot", "Su geçirmez deri, dayanıklı taban, kışlık bot.", 3500, 5500,
                new[] { "https://images.unsplash.com/photo-1520639888713-7851133b1ed0?w=400", "https://images.unsplash.com/photo-1605812860427-4024433a70fd?w=400" }, "Bot"),
            new("Dr. Martens 1460", "İkonik bot, sarı dikiş, deri.", 3500, 5500,
                new[] { "https://images.unsplash.com/photo-1608256246200-53e635b5b65f?w=400", "https://images.unsplash.com/photo-1605733160314-4fc7dac4bb16?w=400" }, "Bot"),
            new("CAT Colorado Bot", "Dayanıklı iş botu, kauçuk taban.", 2500, 4500,
                new[] { "https://images.unsplash.com/photo-1605812860427-4024433a70fd?w=400" }, "Bot"),
            // Sneaker
            new("Converse Chuck Taylor", "Klasik kanvas sneaker, zamansız tasarım.", 800, 1500,
                new[] { "https://images.unsplash.com/photo-1607522370275-f14206abe5d3?w=400", "https://images.unsplash.com/photo-1494496195158-c3becb4f2475?w=400" }, "Sneaker"),
            new("Vans Old Skool", "Klasik skate ayakkabı, süet detaylar.", 1200, 2200,
                new[] { "https://images.unsplash.com/photo-1525966222134-fcfa99b8ae77?w=400", "https://images.unsplash.com/photo-1606107557195-0e29a4b5b4aa?w=400" }, "Sneaker"),
            new("Nike Air Force 1", "İkonik sneaker, deri üst, klasik beyaz.", 2500, 4000,
                new[] { "https://images.unsplash.com/photo-1606107557195-0e29a4b5b4aa?w=400" }, "Sneaker"),
            // Sandalet
            new("Birkenstock Arizona", "Anatomik tabanlık, mantar taban, yaz için.", 1500, 2800,
                new[] { "https://images.unsplash.com/photo-1603487742131-4160ec999306?w=400" }, "Sandalet"),
            new("Crocs Classic Clog", "Hafif, rahat, su geçirmez, renkli.", 600, 1200,
                new[] { "https://images.unsplash.com/photo-1603487742131-4160ec999306?w=400" }, "Sandalet"),
        };

        private List<ProductTemplate> GetKozmetikProducts() => new()
        {
            // Parfüm
            new("Chanel No. 5 Parfüm", "Efsanevi kadın parfümü, çiçeksi notalar, 100ml.", 8000, 15000,
                new[] { "https://images.unsplash.com/photo-1541643600914-78b084683601?w=400", "https://images.unsplash.com/photo-1523293182086-7651a899d37f?w=400" }, "Parfüm"),
            new("Dior Sauvage EDT", "Erkek parfümü, odunsu ve baharatlı, 100ml.", 4500, 8000,
                new[] { "https://images.unsplash.com/photo-1594035910387-fea47794261f?w=400", "https://images.unsplash.com/photo-1590736969955-71cc94901144?w=400" }, "Parfüm"),
            new("Versace Eros", "Erkek parfümü, taze ve güçlü, 100ml.", 3500, 6000,
                new[] { "https://images.unsplash.com/photo-1590736969955-71cc94901144?w=400" }, "Parfüm"),
            new("YSL Black Opium", "Kadın parfümü, vanilya ve kahve notaları.", 5000, 9000,
                new[] { "https://images.unsplash.com/photo-1541643600914-78b084683601?w=400" }, "Parfüm"),
            // Ruj
            new("MAC Ruby Woo Ruj", "Mat kırmızı ruj, uzun süre kalıcı, ikonik renk.", 500, 900,
                new[] { "https://images.unsplash.com/photo-1586495777744-4413f21062fa?w=400", "https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400" }, "Ruj"),
            new("Maybelline SuperStay", "24 saat kalıcı ruj, mat formül.", 200, 400,
                new[] { "https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400" }, "Ruj"),
            new("Charlotte Tilbury Pillow Talk", "Nude pembe ruj, kremsi formül.", 800, 1400,
                new[] { "https://images.unsplash.com/photo-1586495777744-4413f21062fa?w=400" }, "Ruj"),
            // Fondöten
            new("Estee Lauder Double Wear", "24 saat kalıcı fondöten, orta-tam kapatıcılık.", 1200, 2200,
                new[] { "https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400", "https://images.unsplash.com/photo-1631730486572-226d1f595b68?w=400" }, "Fondöten"),
            new("L'Oreal True Match", "Doğal görünüm, hafif formül, SPF 17.", 300, 600,
                new[] { "https://images.unsplash.com/photo-1631730486572-226d1f595b68?w=400" }, "Fondöten"),
            // Maskara
            new("Maybelline Lash Sensational", "Hacim veren maskara, yelpaze etkisi.", 150, 350,
                new[] { "https://images.unsplash.com/photo-1512496015851-a90fb38ba796?w=400", "https://images.unsplash.com/photo-1631214524020-7e18db9a8f92?w=400" }, "Maskara"),
            new("L'Oreal Volume Million", "Yoğun hacim, uzatma etkisi, siyah.", 200, 400,
                new[] { "https://images.unsplash.com/photo-1631214524020-7e18db9a8f92?w=400" }, "Maskara"),
            // Cilt Bakım
            new("La Roche-Posay Serum", "C Vitamini serumu, aydınlatıcı, 30ml.", 800, 1500,
                new[] { "https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=400", "https://images.unsplash.com/photo-1608248597279-f99d160bfcbc?w=400" }, "Cilt Bakım"),
            new("The Ordinary Niacinamide", "%10 Niacinamide + %1 Zinc, gözenek küçültücü.", 200, 400,
                new[] { "https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=400", "https://images.unsplash.com/photo-1617897903246-719242758050?w=400" }, "Cilt Bakım"),
            new("Nivea Nemlendirici Krem", "Klasik mavi kutu, yoğun nemlendirme, 250ml.", 80, 180,
                new[] { "https://images.unsplash.com/photo-1611930022073-b7a4ba5fcccd?w=400", "https://images.unsplash.com/photo-1556228720-195a672e8a03?w=400" }, "Cilt Bakım"),
            new("CeraVe Nemlendirici Losyon", "Seramid içerikli, hassas ciltler için.", 300, 550,
                new[] { "https://images.unsplash.com/photo-1556228720-195a672e8a03?w=400" }, "Cilt Bakım"),
            // Saç Bakım
            new("L'Oreal Elseve Şampuan", "Keratin onarıcı, yıpranmış saçlar için, 450ml.", 100, 250,
                new[] { "https://images.unsplash.com/photo-1535585209827-a15fcdbc4c2d?w=400", "https://images.unsplash.com/photo-1626806787461-102c1bfaaea1?w=400" }, "Saç Bakım"),
            new("Pantene Pro-V Saç Kremi", "Onarıcı ve besleyici, 400ml.", 80, 180,
                new[] { "https://images.unsplash.com/photo-1626806787461-102c1bfaaea1?w=400" }, "Saç Bakım"),
            new("Moroccanoil Treatment", "Argan yağlı saç bakım yağı, 100ml.", 600, 1100,
                new[] { "https://images.unsplash.com/photo-1535585209827-a15fcdbc4c2d?w=400" }, "Saç Bakım"),
            // Makyaj Seti
            new("Urban Decay Naked Palette", "12'li far paleti, nötr tonlar, günlük makyaj.", 1500, 2800,
                new[] { "https://images.unsplash.com/photo-1583241800698-e8ab01830a07?w=400", "https://images.unsplash.com/photo-1512496015851-a90fb38ba796?w=400" }, "Makyaj Seti"),
            new("NYX Ultimate Shadow Palette", "16 renk far, pigmentli, uzun ömürlü.", 400, 800,
                new[] { "https://images.unsplash.com/photo-1512496015851-a90fb38ba796?w=400" }, "Makyaj Seti"),
        };

        private List<ProductTemplate> GetCantaProducts() => new()
        {
            // El Çantası
            new("Michael Kors Jet Set Tote", "Saffiano deri, orta boy, çok gözlü.", 4500, 8000,
                new[] { "https://images.unsplash.com/photo-1584917865442-de89df76afd3?w=400", "https://images.unsplash.com/photo-1590874103328-eac38a683ce7?w=400" }, "El Çantası"),
            new("Coach Tabby Shoulder Bag", "Yumuşak deri, vintage toka, şık tasarım.", 6000, 12000,
                new[] { "https://images.unsplash.com/photo-1566150905458-1bf1fc113f0d?w=400", "https://images.unsplash.com/photo-1594223274512-ad4803739b7c?w=400" }, "El Çantası"),
            new("Longchamp Le Pliage", "Katlanabilir naylon çanta, deri detaylar.", 2500, 4500,
                new[] { "https://images.unsplash.com/photo-1559563458-527698bf5295?w=400", "https://images.unsplash.com/photo-1584917865442-de89df76afd3?w=400" }, "El Çantası"),
            new("Kate Spade Tote Bag", "Deri el çantası, fermuar kapatma, zarif.", 3500, 6500,
                new[] { "https://images.unsplash.com/photo-1590874103328-eac38a683ce7?w=400" }, "El Çantası"),
            // Sırt Çantası
            new("Fjällräven Kånken Sırt Çantası", "İsveç tasarımı, su geçirmez, 16L.", 1500, 2800,
                new[] { "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=400", "https://images.unsplash.com/photo-1581605405669-fcdf81165afa?w=400" }, "Sırt Çantası"),
            new("Herschel Little America", "Klasik tasarım, laptop bölmeli, 25L.", 1800, 3200,
                new[] { "https://images.unsplash.com/photo-1581605405669-fcdf81165afa?w=400" }, "Sırt Çantası"),
            new("Nike Brasilia Sırt Çantası", "Spor sırt çantası, dayanıklı, çok bölmeli.", 600, 1200,
                new[] { "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=400" }, "Sırt Çantası"),
            // Omuz Çantası
            new("Guess Logo Omuz Çantası", "Monogram desen, ayarlanabilir askı.", 2000, 4000,
                new[] { "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?w=400", "https://images.unsplash.com/photo-1591561954557-26941169b49e?w=400" }, "Omuz Çantası"),
            new("Fossil Crossbody Çanta", "Deri, ayarlanabilir askı, kompakt.", 1800, 3500,
                new[] { "https://images.unsplash.com/photo-1591561954557-26941169b49e?w=400", "https://images.unsplash.com/photo-1590874103328-eac38a683ce7?w=400" }, "Omuz Çantası"),
            new("Kipling Defea Çanta", "Hafif naylon, çok gözlü, günlük kullanım.", 1200, 2400,
                new[] { "https://images.unsplash.com/photo-1566150905458-1bf1fc113f0d?w=400", "https://images.unsplash.com/photo-1548036328-c9fa89d128fa?w=400" }, "Omuz Çantası"),
            // Cüzdan
            new("Herschel Supply Cüzdan", "Deri cüzdan, RFID koruma, slim tasarım.", 400, 900,
                new[] { "https://images.unsplash.com/photo-1627123424574-724758594e93?w=400", "https://images.unsplash.com/photo-1606503825008-909a67e63c3d?w=400" }, "Cüzdan"),
            new("Tommy Hilfiger Deri Cüzdan", "Hakiki deri, çoklu kart bölmesi.", 600, 1200,
                new[] { "https://images.unsplash.com/photo-1606503825008-909a67e63c3d?w=400" }, "Cüzdan"),
            new("Guess Kadın Cüzdan", "Logo baskılı, fermuarlı, şık tasarım.", 500, 1000,
                new[] { "https://images.unsplash.com/photo-1627123424574-724758594e93?w=400" }, "Cüzdan"),
            // Bavul
            new("Samsonite Spinner Bavul", "4 tekerlekli, TSA kilit, genişletilebilir, 68cm.", 3500, 6500,
                new[] { "https://images.unsplash.com/photo-1565026057447-bc90a3dceb87?w=400", "https://images.unsplash.com/photo-1581553680321-4fffae59fccd?w=400" }, "Bavul"),
            new("American Tourister Bavul", "Hafif ABS, 4 tekerlek, orta boy.", 1500, 3000,
                new[] { "https://images.unsplash.com/photo-1581553680321-4fffae59fccd?w=400" }, "Bavul"),
            new("Delsey Paris Kabin Boy", "Kabin boyu bavul, TSA kilit, 55cm.", 2000, 4000,
                new[] { "https://images.unsplash.com/photo-1565026057447-bc90a3dceb87?w=400" }, "Bavul"),
            // Laptop Çantası
            new("Targus Laptop Çantası", "15.6 inç laptop uyumlu, yastıklı koruma.", 500, 1200,
                new[] { "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=400", "https://images.unsplash.com/photo-1585916420730-d7f95e942d43?w=400" }, "Laptop Çantası"),
            new("Samsonite Laptop Sırt Çantası", "17 inç laptop bölmeli, business stil.", 1200, 2500,
                new[] { "https://images.unsplash.com/photo-1585916420730-d7f95e942d43?w=400" }, "Laptop Çantası"),
        };

        private List<ProductTemplate> GetKitapProducts() => new()
        {
            // Roman
            new("Suç ve Ceza - Dostoyevski", "Dünya edebiyatının başyapıtı, psikolojik roman.", 45, 120,
                new[] { "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400", "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400" }, "Roman"),
            new("1984 - George Orwell", "Distopik klasik, totaliter rejim eleştirisi.", 35, 90,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400", "https://images.unsplash.com/photo-1476275466078-4007374efbbe?w=400" }, "Roman"),
            new("Sefiller - Victor Hugo", "Fransız edebiyatı klasiği, epik roman.", 70, 160,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400", "https://images.unsplash.com/photo-1476275466078-4007374efbbe?w=400" }, "Roman"),
            new("İnce Memed - Yaşar Kemal", "Türk edebiyatının başyapıtı, Anadolu.", 55, 130,
                new[] { "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400", "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400" }, "Roman"),
            new("Yüzüklerin Efendisi - Tolkien", "Fantastik edebiyatın zirve eseri.", 150, 350,
                new[] { "https://images.unsplash.com/photo-1474366521946-c3d4b507abf2?w=400" }, "Roman"),
            // Kişisel Gelişim
            new("Simyacı - Paulo Coelho", "Kişisel gelişim ve felsefe, dünya bestseller.", 40, 100,
                new[] { "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400", "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400" }, "Kişisel Gelişim"),
            new("Atomik Alışkanlıklar - James Clear", "Kişisel gelişim, alışkanlık oluşturma.", 60, 140,
                new[] { "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400", "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400" }, "Kişisel Gelişim"),
            new("Ikigai - Hector Garcia", "Japon yaşam felsefesi, mutluluk sırrı.", 45, 110,
                new[] { "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400" }, "Kişisel Gelişim"),
            new("Bunu Yapabilirsin - David Schwartz", "Motivasyon ve başarı rehberi.", 50, 120,
                new[] { "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400" }, "Kişisel Gelişim"),
            // Tarih
            new("Nutuk - Atatürk", "Türk tarihinin en önemli eseri.", 50, 150,
                new[] { "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400", "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400" }, "Tarih"),
            new("Sapiens - Yuval Noah Harari", "İnsanlık tarihi, bilim ve tarih.", 80, 180,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400", "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=400" }, "Tarih"),
            new("İstanbul - Orhan Pamuk", "İstanbul'un hikayesi, anılar ve tarih.", 60, 140,
                new[] { "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=400" }, "Tarih"),
            // Bilim
            new("Cosmos - Carl Sagan", "Evren hakkında popüler bilim klasiği.", 70, 160,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400" }, "Bilim"),
            new("Kısa Yanıtlar - Stephen Hawking", "Büyük sorulara kısa yanıtlar.", 55, 130,
                new[] { "https://images.unsplash.com/photo-1495446815901-a7297e633e8d?w=400" }, "Bilim"),
            // Çocuk Kitabı
            new("Küçük Prens - Saint-Exupéry", "Her yaş için klasik, illüstrasyonlu.", 30, 80,
                new[] { "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400", "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?w=400" }, "Çocuk Kitabı"),
            new("Şeker Portakalı - José Mauro", "Çocuk edebiyatı klasiği, dokunaklı.", 35, 85,
                new[] { "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?w=400", "https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=400" }, "Çocuk Kitabı"),
            new("Charlie'nin Çikolata Fabrikası", "Roald Dahl klasiği, macera dolu.", 40, 90,
                new[] { "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?w=400" }, "Çocuk Kitabı"),
            // Edebiyat
            new("Tutunamayanlar - Oğuz Atay", "Modern Türk edebiyatı, postmodern.", 70, 160,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400", "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400" }, "Edebiyat"),
            new("Harry Potter Serisi - J.K. Rowling", "Fantastik edebiyat, tüm yaşlar için.", 200, 500,
                new[] { "https://images.unsplash.com/photo-1474366521946-c3d4b507abf2?w=400", "https://images.unsplash.com/photo-1618666012174-83b441c0bc76?w=400" }, "Edebiyat"),
            // Felsefe
            new("Sofinin Dünyası - Jostein Gaarder", "Felsefe tarihi, giriş kitabı.", 55, 130,
                new[] { "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400" }, "Felsefe"),
            new("Böyle Buyurdu Zerdüşt - Nietzsche", "Felsefe klasiği, aforizmalar.", 45, 110,
                new[] { "https://images.unsplash.com/photo-1543002588-bfa74002ed7e?w=400" }, "Felsefe"),
        };

        private string AddVariation(string name, int index, Random random)
        {
            if (index == 0) return name;
            
            var variations = new[] { "", " - Siyah", " - Beyaz", " - Mavi", " - Kırmızı", " - Gri", " (Yeni)", " (2024)", " - Limited" };
            return name + variations[random.Next(variations.Length)];
        }

        private class ProductTemplate
        {
            public string Name { get; }
            public string Description { get; }
            public double MinPrice { get; }
            public double MaxPrice { get; }
            public string[] Images { get; }
            public string SubCategoryName { get; }  // 🔥 Alt kategori adı

            public ProductTemplate(string name, string description, double minPrice, double maxPrice, string[] images, string subCategory = "")
            {
                Name = name;
                Description = description;
                MinPrice = minPrice;
                MaxPrice = maxPrice;
                Images = images;
                SubCategoryName = subCategory;
            }

            public string GetRandomImage(Random random) => Images[random.Next(Images.Length)];
        }

        /// <summary>
        /// 🌱 1000 adet örnek ürün ekler
        /// GET: api/Seed/products?count=1000
        /// </summary>
        [HttpGet("products")]
        public async Task<IActionResult> SeedProducts([FromQuery] int count = 1000)
        {
            try
            {
                // 🔹 1. Mevcut kategorileri kontrol et
                var categories = await _context.Categories.ToListAsync();
                
                // Eğer kategori yoksa, önce kategorileri oluştur
                if (!categories.Any())
                {
                    await EnsureCategoriesExist();
                    categories = await _context.Categories.ToListAsync();
                }

                // Hala kategori yoksa hata dön
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı ve oluşturulamadı!");

                // 🔹 2. Alt kategorileri al
                var subCategories = await _context.SubCategories.ToListAsync();

                var random = new Random();
                var productsToAdd = new List<Product>();

                // 🔹 3. Ürün verilerini hazırla
                var productData = GetProductData();
                var allTemplateKeys = productData.Keys.ToList();

                for (int i = 0; i < count; i++)
                {
                    // Rastgele kategori seç
                    var category = categories[random.Next(categories.Count)];
                    var categoryName = category.Name?.ToLower() ?? "";

                    // O kategoriye ait alt kategorileri bul (varsa)
                    var relevantSubs = subCategories.Where(s => s.CategoryId == category.Id).ToList();
                    int? subCategoryId = relevantSubs.Any() ? relevantSubs[random.Next(relevantSubs.Count)].Id : null;

                    // Kategoriye göre ürün ismi ve açıklama seç
                    var (name, description) = GetProductNameAndDescription(categoryName, productData, random, i, allTemplateKeys);

                    // Rastgele fiyat (50 - 15000 TL arası)
                    var price = Math.Round((decimal)(random.NextDouble() * 14950 + 50), 2);

                    // Rastgele stok (0 - 500 arası)
                    var stock = random.Next(0, 501);

                    // Resim URL'si - kategoriye göre resimler (loremflickr)
                    var imageUrl = GetCategoryImageUrl(categoryName, i);

                    var product = new Product
                    {
                        Name = name,
                        Description = description,
                        Price = price,
                        Stock = stock,
                        CategoryId = category.Id,
                        SubCategoryId = subCategoryId,
                        ImageUrl = imageUrl,
                        Status = stock > 0 ? "Stokta var" : "Tükendi"
                    };

                    productsToAdd.Add(product);
                }

                // 🔹 4. Toplu ekleme (performans için)
                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ {count} adet ürün başarıyla eklendi!",
                    totalProducts = await _context.Products.CountAsync(),
                    categoriesUsed = categories.Select(c => new { c.Id, c.Name }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace });
            }
        }

        /// <summary>
        /// 🗑️ Tüm seed ürünlerini siler (tüm harici resim kaynaklarını içerir)
        /// </summary>
        /// <summary>
        /// 🗑️ Seed'lenen ürünleri siler (sadece API'den eklenenler)
        /// DELETE: api/Seed/products
        /// </summary>
        [HttpDelete("products")]
        public async Task<IActionResult> DeleteSeedProducts()
        {
            var seedProducts = await _context.Products
                .Where(p => p.ImageUrl != null && 
                    (p.ImageUrl.Contains("picsum.photos") || 
                     p.ImageUrl.Contains("loremflickr") ||
                     p.ImageUrl.Contains("dummyjson") ||
                     p.ImageUrl.Contains("i.dummyjson") ||
                     p.ImageUrl.Contains("cdn.dummyjson") ||
                     p.ImageUrl.Contains("fakestoreapi") ||
                     p.ImageUrl.Contains("unsplash.com") ||
                     p.ImageUrl.Contains("source.unsplash.com") ||
                     p.ImageUrl.Contains("via.placeholder.com")))
                .ToListAsync();

            _context.Products.RemoveRange(seedProducts);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"🗑️ {seedProducts.Count} adet seed ürün silindi." });
        }

        /// <summary>
        /// 🗑️ TÜM ürünleri siler (dikkatli kullan!)
        /// DELETE: api/Seed/products/all
        /// </summary>
        [HttpDelete("products/all")]
        public async Task<IActionResult> DeleteAllProducts()
        {
            var allProducts = await _context.Products.ToListAsync();
            var count = allProducts.Count;

            _context.Products.RemoveRange(allProducts);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"🗑️ Tüm ürünler silindi! ({count} adet)" });
        }

        /// <summary>
        /// 🔥 YENİ YAKLAŞIM: Placeholder.com + Fake Store API kombinasyonu
        /// GET: api/Seed/from-multiple-sources?countPerCategory=200
        /// Her kategori için farklı resim servisleri kullanır, her ürün için benzersiz resimler
        /// </summary>
        [HttpGet("from-multiple-sources")]
        public async Task<IActionResult> SeedFromMultipleSources([FromQuery] int countPerCategory = 150)
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                // 1. Fake Store API'den ürünler çek (DummyJSON'dan farklı bir kaynak)
                var fakeStoreResponse = await _httpClient.GetAsync("https://fakestoreapi.com/products");
                var fakeStoreProducts = new List<FakeStoreProduct>();
                
                if (fakeStoreResponse.IsSuccessStatusCode)
                {
                    var fakeStoreJson = await fakeStoreResponse.Content.ReadAsStringAsync();
                    fakeStoreProducts = JsonSerializer.Deserialize<List<FakeStoreProduct>>(fakeStoreJson, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    }) ?? new List<FakeStoreProduct>();
                }

                // 2. DummyJSON'dan da ürünler çek
                var dummyJsonResponse = await _httpClient.GetAsync("https://dummyjson.com/products?limit=1000");
                var dummyJsonProducts = new List<DummyJsonProduct>();
                
                if (dummyJsonResponse.IsSuccessStatusCode)
                {
                    var dummyJson = await dummyJsonResponse.Content.ReadAsStringAsync();
                    var dummyData = JsonSerializer.Deserialize<DummyJsonResponse>(dummyJson, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    dummyJsonProducts = dummyData?.Products ?? new List<DummyJsonProduct>();
                }

                // Kategori eşleştirme: Fake Store kategorileri -> Bizim kategorilerimiz
                var fakeStoreCategoryMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Elektronik", new List<string> { "electronics" } },
                    { "Giyim", new List<string> { "men's clothing", "women's clothing" } },
                    { "Ayakkabı", new List<string> { "men's clothing", "women's clothing" } },
                    { "Kozmetik", new List<string> { "jewelery" } },
                    { "Çanta", new List<string> { "jewelery" } },
                    { "Kitap", new List<string> { "electronics" } }
                };

                // Kategori eşleştirme: DummyJSON kategorileri -> Bizim kategorilerimiz
                var dummyJsonCategoryMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Elektronik", new List<string> { "smartphones", "laptops", "tablets", "mobile-accessories", "automotive", "motorcycle", "lighting" } },
                    { "Giyim", new List<string> { "tops", "womens-dresses", "mens-shirts", "mens-watches", "womens-watches", "womens-jewellery", "sunglasses" } },
                    { "Ayakkabı", new List<string> { "womens-shoes", "mens-shoes" } },
                    { "Kozmetik", new List<string> { "fragrances", "skincare", "beauty", "skin-care" } },
                    { "Çanta", new List<string> { "womens-bags", "mens-bags" } },
                    { "Kitap", new List<string> { "groceries", "home-decoration", "furniture" } }
                };

                var random = new Random();
                var productsToAdd = new List<Product>();
                var categoryStats = new Dictionary<string, int>();
                var globalCounter = 0;

                foreach (var category in categories)
                {
                    var catName = category.Name ?? "";
                    var categoryHash = Math.Abs(catName.GetHashCode());

                    // Bu kategoriye uygun Fake Store ürünlerini filtrele
                    var matchingFakeStoreCategories = fakeStoreCategoryMap.ContainsKey(catName) 
                        ? fakeStoreCategoryMap[catName] 
                        : new List<string>();
                    
                    var matchingFakeProducts = fakeStoreProducts
                        .Where(p => matchingFakeStoreCategories.Any(c => 
                            p.Category != null && p.Category.Equals(c, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    // Bu kategoriye uygun DummyJSON ürünlerini filtrele
                    var matchingDummyCategories = dummyJsonCategoryMap.ContainsKey(catName) 
                        ? dummyJsonCategoryMap[catName] 
                        : new List<string>();
                    
                    var matchingDummyProducts = dummyJsonProducts
                        .Where(p => matchingDummyCategories.Any(c => 
                            p.Category != null && p.Category.Equals(c, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    // Eğer eşleşen ürün yoksa, tüm ürünlerden seç
                    if (!matchingFakeProducts.Any())
                        matchingFakeProducts = fakeStoreProducts.ToList();
                    if (!matchingDummyProducts.Any())
                        matchingDummyProducts = dummyJsonProducts.ToList();

                    for (int i = 0; i < countPerCategory; i++)
                    {
                        globalCounter++;
                        
                        // Her ürün için benzersiz bir ID oluştur
                        var uniqueId = Math.Abs(
                            (globalCounter * 7919L) + 
                            (i * 17L) + 
                            (categoryHash * 31L) + 
                            (DateTime.Now.Ticks % 1000000L) +
                            random.Next(1000000)
                        );

                        // Fake Store veya DummyJSON'dan KATEGORİYE UYGUN ürün seç
                        string productName = "";
                        string description = "";
                        decimal price = 0;
                        string imageUrl = "";

                        // %50 Fake Store, %50 DummyJSON (her ikisi de kategoriye uygun)
                        if (random.Next(2) == 0 && matchingFakeProducts.Any())
                        {
                            var fakeProduct = matchingFakeProducts[(int)(uniqueId % matchingFakeProducts.Count)];
                            productName = fakeProduct.Title ?? "Ürün";
                            description = fakeProduct.Description ?? "Kaliteli ürün";
                            price = (decimal)(fakeProduct.Price ?? 0) * 34.5m;
                            imageUrl = fakeProduct.Image ?? "";
                        }
                        else if (matchingDummyProducts.Any())
                        {
                            var dummyProduct = matchingDummyProducts[(int)(uniqueId % matchingDummyProducts.Count)];
                            productName = dummyProduct.Title ?? "Ürün";
                            description = dummyProduct.Description ?? "Kaliteli ürün";
                            price = (decimal)(dummyProduct.Price ?? 0) * 34.5m;
                            
                            // DummyJSON'dan resim seç (her ürün için farklı)
                            if (dummyProduct.Images != null && dummyProduct.Images.Any())
                            {
                                var imageIndex = (int)(uniqueId % dummyProduct.Images.Count);
                                imageUrl = dummyProduct.Images[imageIndex];
                            }
                            else
                            {
                                imageUrl = dummyProduct.Thumbnail ?? "";
                            }
                        }

                        // Her ürün için benzersiz resim: Kategoriye göre farklı servisler
                        if (string.IsNullOrEmpty(imageUrl))
                        {
                            // Her kategori için farklı resim servisi ve benzersiz ID
                            var categoryImageId = (uniqueId % 10000) + (categoryHash % 1000);
                            
                            imageUrl = catName.ToLower() switch
                            {
                                var c when c.Contains("elektronik") => $"https://picsum.photos/seed/tech{categoryImageId}/400/400",
                                var c when c.Contains("giyim") => $"https://picsum.photos/seed/fashion{categoryImageId}/400/400",
                                var c when c.Contains("ayakkab") => $"https://picsum.photos/seed/shoes{categoryImageId}/400/400",
                                var c when c.Contains("kozmetik") => $"https://picsum.photos/seed/beauty{categoryImageId}/400/400",
                                var c when c.Contains("çanta") || c.Contains("canta") => $"https://picsum.photos/seed/bag{categoryImageId}/400/400",
                                var c when c.Contains("kitap") => $"https://picsum.photos/seed/book{categoryImageId}/400/400",
                                _ => $"https://picsum.photos/seed/product{categoryImageId}/400/400"
                            };
                        }
                        else
                        {
                            // Mevcut resme benzersiz parametre ekle (cache bypass)
                            var separator = imageUrl.Contains("?") ? "&" : "?";
                            imageUrl = $"{imageUrl}{separator}v={uniqueId}&t={DateTime.Now.Ticks}&sig={uniqueId * 17}";
                        }

                        // Ürün adını varyasyonla
                        productName = AddVariation(productName, i, random);
                        price = Math.Round(price * (0.8m + (decimal)random.NextDouble() * 0.4m), 2);

                        var product = new Product
                        {
                            Name = productName,
                            Description = description,
                            Price = price,
                            Stock = random.Next(5, 300),
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            ImageUrl = imageUrl,
                            Status = "Stokta var"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[catName] = countPerCategory;
                }

                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ Toplam {productsToAdd.Count} ürün eklendi! (Fake Store + DummyJSON + Placeholder)",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    sources = new[] { "Fake Store API", "DummyJSON API", "Placeholder.com" }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        // Fake Store API modeli
        private class FakeStoreProduct
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public decimal? Price { get; set; }
            public string? Description { get; set; }
            public string? Image { get; set; }
            public string? Category { get; set; }
        }

        /// <summary>
        /// 🔥 DummyJSON'dan TÜM kategorileri çekip, senin kategorilerine göre eşleştirir
        /// GET: api/Seed/from-dummyjson-all?countPerCategory=200
        /// Her kategori için DummyJSON'dan TÜM ürünleri çeker, her ürün için farklı resimler
        /// </summary>
        [HttpGet("from-dummyjson-all")]
        public async Task<IActionResult> SeedFromDummyJsonAllCategories([FromQuery] int countPerCategory = 150)
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                // DummyJSON'dan TÜM ürünleri çek (1000 ürün)
                var allProductsResponse = await _httpClient.GetAsync("https://dummyjson.com/products?limit=1000");
                if (!allProductsResponse.IsSuccessStatusCode)
                    return StatusCode(500, new { error = "DummyJSON API'ye bağlanılamadı." });

                var allProductsJson = await allProductsResponse.Content.ReadAsStringAsync();
                var allProductsData = JsonSerializer.Deserialize<DummyJsonResponse>(allProductsJson, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (allProductsData?.Products == null || !allProductsData.Products.Any())
                    return BadRequest("API'den ürün alınamadı.");

                // Kategori eşleştirme: DummyJSON kategorileri -> Bizim kategorilerimiz
                var categoryMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Elektronik", new List<string> { "smartphones", "laptops", "tablets", "mobile-accessories", "automotive", "motorcycle", "lighting" } },
                    { "Giyim", new List<string> { "tops", "womens-dresses", "mens-shirts", "mens-watches", "womens-watches", "womens-jewellery", "sunglasses" } },
                    { "Ayakkabı", new List<string> { "womens-shoes", "mens-shoes" } },
                    { "Kozmetik", new List<string> { "fragrances", "skincare", "beauty", "skin-care" } },
                    { "Çanta", new List<string> { "womens-bags", "mens-bags" } },
                    { "Kitap", new List<string> { "groceries", "home-decoration", "furniture" } }
                };

                var random = new Random();
                var productsToAdd = new List<Product>();
                var categoryStats = new Dictionary<string, int>();
                var usedImageUrls = new HashSet<string>(); // Global olarak kullanılan resimleri takip et
                var usedProductIds = new HashSet<int>(); // Kullanılan ürün ID'lerini takip et (aynı ürünü tekrar kullanma)
                var globalProductCounter = 0; // Global ürün sayacı (her ürün için benzersiz)

                // Her kategori için ürünler oluştur
                foreach (var category in categories)
                {
                    var catName = category.Name ?? "";
                    
                    // Bu kategoriye uygun DummyJSON kategorilerini bul
                    var matchingDummyCategories = categoryMap.ContainsKey(catName) 
                        ? categoryMap[catName] 
                        : new List<string>();

                    // Bu kategorilere uygun ürünleri filtrele
                    var matchingProducts = allProductsData.Products
                        .Where(p => matchingDummyCategories.Any(dc => 
                            p.Category != null && p.Category.Equals(dc, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    // Eğer eşleşen ürün yoksa, tüm ürünlerden rastgele seç
                    if (!matchingProducts.Any())
                        matchingProducts = allProductsData.Products.ToList();

                    // Bu kategoriden countPerCategory kadar ürün oluştur
                    for (int i = 0; i < countPerCategory; i++)
                    {
                        globalProductCounter++;
                        
                        // Her ürün için TAMAMEN BENZERSİZ bir seed oluştur
                        var categoryHash = Math.Abs(catName.GetHashCode());
                        var timestamp = DateTime.Now.Ticks;
                        var uniqueProductSeed = Math.Abs(
                            (globalProductCounter * 7919L) + 
                            (i * 17L) + 
                            (categoryHash * 31L) + 
                            (timestamp * 7L) +
                            (random.Next(1000000) * 13L)
                        );

                        // Farklı bir ürün seç (aynı ürünü tekrar kullanma)
                        DummyJsonProduct apiProduct = null;
                        var productAttempts = 0;
                        while (productAttempts < matchingProducts.Count && (apiProduct == null || usedProductIds.Contains(apiProduct.Id)))
                        {
                            var productIndex = (int)((uniqueProductSeed + productAttempts) % matchingProducts.Count);
                            apiProduct = matchingProducts[productIndex];
                            productAttempts++;
                        }
                        
                        // Eğer tüm ürünler kullanıldıysa, yine de bir ürün seç
                        if (apiProduct == null)
                            apiProduct = matchingProducts[(int)(uniqueProductSeed % matchingProducts.Count)];
                        
                        usedProductIds.Add(apiProduct.Id);
                        
                        // Fiyatı TL'ye çevir
                        var priceInTL = Math.Round((apiProduct.Price ?? 100) * 34.5m * (0.8m + (decimal)random.NextDouble() * 0.4m), 2);
                        var stock = random.Next(5, 300);

                        // Ürün adını varyasyonla
                        var productName = AddVariation(apiProduct.Title ?? "Ürün", i, random);

                        // Resim: Her ürün için TAMAMEN BENZERSİZ bir resim seç
                        string imageUrl = null;
                        var imageAttempts = 0;
                        var maxImageAttempts = 20;

                        // Strateji: %50 DummyJSON, %50 Picsum Photos (daha fazla çeşitlilik için)
                        var usePicsum = (uniqueProductSeed % 2) == 0;

                        if (!usePicsum)
                        {
                            // DummyJSON resimlerini kullan
                            while (imageAttempts < maxImageAttempts && (string.IsNullOrEmpty(imageUrl) || usedImageUrls.Contains(imageUrl)))
                            {
                                // Ürünün kendi resimlerinden seç
                                if (apiProduct.Images != null && apiProduct.Images.Any())
                                {
                                    var imageIndex = (int)((uniqueProductSeed + imageAttempts) % apiProduct.Images.Count);
                                    imageUrl = apiProduct.Images[imageIndex];
                                }
                                
                                // Eğer resim yoksa, thumbnail kullan
                                if (string.IsNullOrEmpty(imageUrl))
                                    imageUrl = apiProduct.Thumbnail;
                                
                                // Eğer bu resim daha önce kullanıldıysa, benzersiz seed ekle
                                if (!string.IsNullOrEmpty(imageUrl) && usedImageUrls.Contains(imageUrl))
                                {
                                    var uniqueImageSeed = uniqueProductSeed + (imageAttempts * 1000) + random.Next(10000);
                                    imageUrl = $"{imageUrl}?sig={uniqueImageSeed}";
                                }
                                
                                imageAttempts++;
                            }
                        }

                        // Eğer hala resim yoksa veya Picsum kullanılacaksa, Picsum Photos kullan
                        if (string.IsNullOrEmpty(imageUrl) || usedImageUrls.Contains(imageUrl) || usePicsum)
                        {
                            // Her ürün için benzersiz bir Picsum seed
                            var picsumSeed = Math.Abs(
                                (globalProductCounter * 7919L) + 
                                (i * 17L) + 
                                (categoryHash * 31L) + 
                                (timestamp * 7L) +
                                (random.Next(1000000) * 13L) +
                                (imageAttempts * 1000L)
                            );
                            imageUrl = $"https://picsum.photos/400/400?random={picsumSeed}";
                        }

                        usedImageUrls.Add(imageUrl);

                        var product = new Product
                        {
                            Name = productName,
                            Description = apiProduct.Description ?? "Kaliteli ve uygun fiyatlı ürün.",
                            Price = priceInTL,
                            Stock = stock,
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            ImageUrl = imageUrl,
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[catName] = countPerCategory;
                }

                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ Toplam {productsToAdd.Count} ürün DummyJSON API'den eklendi!",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    source = "DummyJSON API (Tüm Kategoriler)",
                    note = "Her ürün için benzersiz resimler, kategorilere göre eşleştirildi"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// 🔥 DummyJSON API'den kategoriye göre ürünler çeker (ÖNERİLEN)
        /// GET: api/Seed/from-dummyjson?countPerCategory=200
        /// Her kategori için DummyJSON'dan ürünler çeker, kategoriye uygun resimlerle
        /// </summary>
        [HttpGet("from-dummyjson")]
        public async Task<IActionResult> SeedFromDummyJsonByCategory([FromQuery] int countPerCategory = 150)
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                // Kategori eşleştirme: DummyJSON kategorileri -> Bizim kategorilerimiz
                var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "smartphones", "Elektronik" },
                    { "laptops", "Elektronik" },
                    { "tablets", "Elektronik" },
                    { "mobile-accessories", "Elektronik" },
                    { "tops", "Giyim" },
                    { "womens-dresses", "Giyim" },
                    { "mens-shirts", "Giyim" },
                    { "womens-shoes", "Ayakkabı" },
                    { "mens-shoes", "Ayakkabı" },
                    { "fragrances", "Kozmetik" },
                    { "skincare", "Kozmetik" },
                    { "womens-bags", "Çanta" },
                    { "groceries", "Kitap" } // DummyJSON'da kitap yok, groceries kullanıyoruz
                };

                var random = new Random();
                var productsToAdd = new List<Product>();
                var categoryStats = new Dictionary<string, int>();

                // Her kategori için DummyJSON'dan ürünler çek
                foreach (var category in categories)
                {
                    var catName = category.Name ?? "";
                    var dummyJsonCategory = categoryMap.FirstOrDefault(x => x.Value.Equals(catName, StringComparison.OrdinalIgnoreCase)).Key;
                    
                    if (string.IsNullOrEmpty(dummyJsonCategory))
                    {
                        // Eğer eşleşme yoksa, tüm ürünlerden rastgele seç
                        dummyJsonCategory = "all";
                    }

                    // DummyJSON'dan MÜMKÜN OLDUĞUNCA FAZLA ürün çek (daha fazla çeşitlilik için)
                    // Önce kategoriye özel çek, yoksa tüm ürünlerden çek
                    var allApiProducts = new List<DummyJsonProduct>();
                    
                    // 1. Kategoriye özel ürünler çek
                    if (dummyJsonCategory != "all")
                    {
                        var categoryUrl = $"https://dummyjson.com/products/category/{dummyJsonCategory}?limit=100";
                        var categoryResponse = await _httpClient.GetAsync(categoryUrl);
                        if (categoryResponse.IsSuccessStatusCode)
                        {
                            var categoryJson = await categoryResponse.Content.ReadAsStringAsync();
                            var categoryData = JsonSerializer.Deserialize<DummyJsonResponse>(categoryJson, new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });
                            if (categoryData?.Products != null)
                                allApiProducts.AddRange(categoryData.Products);
                        }
                    }
                    
                    // 2. Tüm ürünlerden de çek (daha fazla çeşitlilik için)
                    var allProductsUrl = "https://dummyjson.com/products?limit=1000"; // Maksimum limit
                    var allResponse = await _httpClient.GetAsync(allProductsUrl);
                    if (allResponse.IsSuccessStatusCode)
                    {
                        var allJson = await allResponse.Content.ReadAsStringAsync();
                        var allData = JsonSerializer.Deserialize<DummyJsonResponse>(allJson, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        if (allData?.Products != null)
                        {
                            // Kategoriye uygun olanları ekle
                            foreach (var product in allData.Products)
                            {
                                if (!allApiProducts.Any(p => p.Id == product.Id))
                                    allApiProducts.Add(product);
                            }
                        }
                    }

                    if (!allApiProducts.Any())
                        continue;

                    // Bu kategoriden countPerCategory kadar ürün oluştur
                    var usedImageUrls = new HashSet<string>(); // Aynı resimlerin tekrarını önlemek için
                    
                    for (int i = 0; i < countPerCategory; i++)
                    {
                        // Her ürün için farklı bir index ile ürün seç
                        var productIndex = (i * 7919 + random.Next(1000)) % allApiProducts.Count;
                        var apiProduct = allApiProducts[productIndex];
                        
                        // Fiyatı TL'ye çevir (API USD cinsinden)
                        var priceInTL = Math.Round((apiProduct.Price ?? 100) * 34.5m * (0.8m + (decimal)random.NextDouble() * 0.4m), 2);
                        var stock = random.Next(5, 300);

                        // Ürün adını varyasyonla
                        var productName = AddVariation(apiProduct.Title ?? "Ürün", i, random);

                        // Resim: Her ürün için TAMAMEN BENZERSİZ bir resim seç
                        // Strateji: %70 DummyJSON resimleri, %30 Picsum Photos (daha fazla çeşitlilik için)
                        string imageUrl = null;
                        var usePicsum = random.Next(10) < 3; // %30 ihtimalle Picsum Photos kullan
                        
                        if (!usePicsum && apiProduct.Images != null && apiProduct.Images.Any())
                        {
                            // DummyJSON'dan resim seç
                            var imageIndex = (i * 17 + random.Next(1000)) % apiProduct.Images.Count;
                            imageUrl = apiProduct.Images[imageIndex];
                            
                            // Eğer bu resim daha önce kullanıldıysa, benzersiz seed ekle
                            if (usedImageUrls.Contains(imageUrl))
                            {
                                var uniqueSeed = (i * 7919L) + (random.Next(1000000) * 17L) + (catName.GetHashCode() * 31L) + (DateTime.Now.Ticks % 1000000);
                                imageUrl = $"{imageUrl}?sig={uniqueSeed}";
                            }
                        }
                        else if (!usePicsum && !string.IsNullOrEmpty(apiProduct.Thumbnail))
                        {
                            // Thumbnail kullan
                            imageUrl = apiProduct.Thumbnail;
                            if (usedImageUrls.Contains(imageUrl))
                            {
                                var uniqueSeed = (i * 7919L) + (random.Next(1000000) * 17L) + (catName.GetHashCode() * 31L) + (DateTime.Now.Ticks % 1000000);
                                imageUrl = $"{imageUrl}?sig={uniqueSeed}";
                            }
                        }
                        
                        // Eğer hala resim yoksa veya Picsum kullanılacaksa
                        if (string.IsNullOrEmpty(imageUrl) || usePicsum)
                        {
                            // Picsum Photos'tan benzersiz resim al
                            var uniqueSeed = Math.Abs(
                                (i * 7919L) + 
                                (random.Next(1000000) * 17L) + 
                                (catName.GetHashCode() * 31L) + 
                                (DateTime.Now.Ticks % 1000000)
                            );
                            imageUrl = $"https://picsum.photos/400/400?random={uniqueSeed}";
                        }
                        
                        usedImageUrls.Add(imageUrl);

                        var product = new Product
                        {
                            Name = productName,
                            Description = apiProduct.Description ?? "Kaliteli ve uygun fiyatlı ürün.",
                            Price = priceInTL,
                            Stock = stock,
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            ImageUrl = imageUrl ?? "https://via.placeholder.com/400",
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[catName] = countPerCategory;
                }

                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ Toplam {productsToAdd.Count} ürün DummyJSON API'den eklendi!",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    source = "DummyJSON API",
                    note = "Her ürün için farklı resimler DummyJSON'dan geliyor"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// 🌐 DummyJSON API'den gerçek ürün verileri çeker
        /// GET: api/Seed/from-api?multiply=5
        /// multiply: Her ürünü kaç kez çoğaltacağını belirtir (100 ürün * 5 = 500 ürün)
        /// </summary>
        [HttpGet("from-api")]
        public async Task<IActionResult> SeedFromDummyJson([FromQuery] int multiply = 5)
        {
            try
            {
                // 🔹 1. DummyJSON API'den tüm ürünleri çek (100 ürün)
                var response = await _httpClient.GetAsync("https://dummyjson.com/products?limit=100");
                
                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, new { error = "DummyJSON API'ye bağlanılamadı." });

                var json = await response.Content.ReadAsStringAsync();
                var apiData = JsonSerializer.Deserialize<DummyJsonResponse>(json, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (apiData?.Products == null || !apiData.Products.Any())
                    return BadRequest("API'den ürün alınamadı.");

                // 🔹 2. Mevcut kategorileri al
                var categories = await _context.Categories.ToListAsync();
                var subCategories = await _context.SubCategories.ToListAsync();

                if (!categories.Any())
                    return BadRequest("Veritabanında kategori bulunamadı!");

                // 🔹 3. Kategori eşleştirme (Kullanıcının kategorilerine göre)
                // Kategoriler: Elektronik, Giyim, Ayakkabı, Kozmetik, Çanta, Kitap
                var categoryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Elektronik
                    { "smartphones", "Elektronik" },
                    { "laptops", "Elektronik" },
                    { "tablets", "Elektronik" },
                    { "mobile-accessories", "Elektronik" },
                    { "automotive", "Elektronik" },
                    { "motorcycle", "Elektronik" },
                    
                    // Giyim
                    { "tops", "Giyim" },
                    { "womens-dresses", "Giyim" },
                    { "mens-shirts", "Giyim" },
                    { "mens-watches", "Giyim" },
                    { "womens-watches", "Giyim" },
                    { "womens-jewellery", "Giyim" },
                    { "sunglasses", "Giyim" },
                    
                    // Ayakkabı
                    { "womens-shoes", "Ayakkabı" },
                    { "mens-shoes", "Ayakkabı" },
                    
                    // Kozmetik
                    { "fragrances", "Kozmetik" },
                    { "skincare", "Kozmetik" },
                    { "beauty", "Kozmetik" },
                    { "skin-care", "Kozmetik" },
                    
                    // Çanta
                    { "womens-bags", "Çanta" },
                    
                    // Kitap (DummyJSON'da kitap yok, furniture'ı Kitap'a alalım)
                    { "groceries", "Kitap" },
                    { "home-decoration", "Kitap" },
                    { "furniture", "Kitap" },
                    { "lighting", "Kitap" }
                };

                var random = new Random();
                var productsToAdd = new List<Product>();

                // 🔹 4. Her ürünü multiply kadar çoğalt
                for (int m = 0; m < multiply; m++)
                {
                    foreach (var apiProduct in apiData.Products)
                    {
                        // Kategori eşleştir
                        var categoryName = "Elektronik"; // varsayılan
                        if (categoryMap.TryGetValue(apiProduct.Category ?? "", out var mappedCategory))
                        {
                            categoryName = mappedCategory;
                        }

                        var category = categories.FirstOrDefault(c => 
                            c.Name != null && c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                            ?? categories.First();

                        // Alt kategori bul
                        var relevantSubs = subCategories.Where(s => s.CategoryId == category.Id).ToList();
                        int? subCategoryId = relevantSubs.Any() ? relevantSubs[random.Next(relevantSubs.Count)].Id : null;

                        // Fiyatı TL'ye çevir (API USD cinsinden)
                        var priceInTL = Math.Round((apiProduct.Price ?? 100) * 34.5m * (0.8m + (decimal)random.NextDouble() * 0.4m), 2);

                        // Stok belirle
                        var stock = apiProduct.Stock ?? random.Next(10, 200);
                        if (m > 0) stock = random.Next(10, 500); // Çoğaltılmış ürünler için rastgele stok

                        // Resim URL'si (thumbnail veya ilk resim)
                        var imageUrl = apiProduct.Thumbnail;
                        if (apiProduct.Images != null && apiProduct.Images.Any())
                        {
                            imageUrl = apiProduct.Images[random.Next(apiProduct.Images.Count)];
                        }

                        // Ürün adını Türkçeleştir ve çeşitlendir
                        var productName = TranslateProductName(apiProduct.Title ?? "Ürün", m, random);

                        var product = new Product
                        {
                            Name = productName,
                            Description = apiProduct.Description ?? "Kaliteli ve uygun fiyatlı ürün.",
                            Price = priceInTL,
                            Stock = stock,
                            CategoryId = category.Id,
                            SubCategoryId = subCategoryId,
                            ImageUrl = imageUrl ?? "https://via.placeholder.com/400",
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }
                }

                // 🔹 5. Veritabanına kaydet
                await _context.Products.AddRangeAsync(productsToAdd);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"✅ {productsToAdd.Count} adet ürün API'den çekilip eklendi!",
                    totalProducts = await _context.Products.CountAsync(),
                    source = "DummyJSON API",
                    categoriesUsed = categories.Select(c => c.Name).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Ürün adını Türkçeleştir ve çeşitlendir
        /// </summary>
        private string TranslateProductName(string originalName, int iteration, Random random)
        {
            // Basit çeviriler
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "iPhone", "iPhone" },
                { "Samsung", "Samsung" },
                { "MacBook", "MacBook" },
                { "Laptop", "Laptop" },
                { "Phone", "Telefon" },
                { "Watch", "Saat" },
                { "Bag", "Çanta" },
                { "Dress", "Elbise" },
                { "Shirt", "Gömlek" },
                { "Shoes", "Ayakkabı" },
                { "Perfume", "Parfüm" },
                { "Cream", "Krem" },
                { "Oil", "Yağ" },
                { "Serum", "Serum" },
                { "Ring", "Yüzük" },
                { "Necklace", "Kolye" },
                { "Earrings", "Küpe" },
                { "Sunglasses", "Güneş Gözlüğü" },
                { "Chair", "Sandalye" },
                { "Table", "Masa" },
                { "Lamp", "Lamba" },
                { "Light", "Aydınlatma" }
            };

            var name = originalName;
            foreach (var kvp in translations)
            {
                name = name.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Çoğaltma için varyasyon ekle
            if (iteration > 0)
            {
                var suffixes = new[] { "Pro", "Plus", "Max", "Lite", "Ultra", "Mini", "SE", "X", "V2", "Premium" };
                var colors = new[] { "Siyah", "Beyaz", "Mavi", "Kırmızı", "Yeşil", "Gri", "Gold", "Silver" };
                
                if (random.Next(2) == 0)
                    name += $" {suffixes[random.Next(suffixes.Length)]}";
                else
                    name += $" - {colors[random.Next(colors.Length)]}";
            }

            return name;
        }

        // DummyJSON API response modelleri
        private class DummyJsonResponse
        {
            public List<DummyJsonProduct>? Products { get; set; }
            public int Total { get; set; }
        }

        private class DummyJsonProduct
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public decimal? Price { get; set; }
            public string? Category { get; set; }
            public string? Thumbnail { get; set; }
            public List<string>? Images { get; set; }
            public int? Stock { get; set; }
            public string? Brand { get; set; }
        }

        /// <summary>
        /// 📊 Mevcut ürün sayısını gösterir
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var totalProducts = await _context.Products.CountAsync();
            var totalCategories = await _context.Categories.CountAsync();
            var totalSubCategories = await _context.SubCategories.CountAsync();

            var productsByCategory = await _context.Products
                .GroupBy(p => p.Category.Name)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new
            {
                totalProducts,
                totalCategories,
                totalSubCategories,
                productsByCategory
            });
        }

        /// <summary>
        /// Kategoriler ve alt kategoriler yoksa oluşturur
        /// </summary>
        private async Task EnsureCategoriesExist()
        {
            // Eğer kategori varsa çık
            if (await _context.Categories.AnyAsync())
                return;

            var categoriesData = new Dictionary<string, List<string>>
            {
                { "Elektronik", new List<string> { "Telefon", "Laptop", "Tablet", "Kulaklık", "Akıllı Saat", "Kamera", "Televizyon" } },
                { "Giyim", new List<string> { "T-Shirt", "Pantolon", "Ceket", "Elbise", "Gömlek", "Kazak", "Mont" } },
                { "Ayakkabı", new List<string> { "Spor Ayakkabı", "Günlük Ayakkabı", "Bot", "Sandalet", "Topuklu" } },
                { "Kozmetik", new List<string> { "Parfüm", "Ruj", "Fondöten", "Maskara", "Cilt Bakım", "Saç Bakım" } },
                { "Kitap", new List<string> { "Roman", "Kişisel Gelişim", "Tarih", "Bilim", "Çocuk Kitapları", "Edebiyat" } },
                { "Aksesuar", new List<string> { "Çanta", "Saat", "Gözlük", "Takı", "Kemer", "Cüzdan" } },
                { "Ev & Yaşam", new List<string> { "Mobilya", "Dekorasyon", "Mutfak", "Banyo", "Aydınlatma" } },
                { "Spor", new List<string> { "Fitness", "Outdoor", "Takım Sporları", "Su Sporları", "Bisiklet" } }
            };

            foreach (var categoryData in categoriesData)
            {
                var category = new Category { Name = categoryData.Key };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                foreach (var subName in categoryData.Value)
                {
                    _context.SubCategories.Add(new SubCategory
                    {
                        Name = subName,
                        CategoryId = category.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Kategorilere göre ürün verileri
        /// </summary>
        private Dictionary<string, ProductTemplates> GetProductData()
        {
            return new Dictionary<string, ProductTemplates>
            {
                ["elektronik"] = new ProductTemplates
                {
                    Brands = new[] { "Samsung", "Apple", "Xiaomi", "Huawei", "Sony", "LG", "Asus", "Lenovo", "HP", "Dell", "Oppo", "Realme", "OnePlus", "JBL", "Bose", "Philips", "Anker", "Logitech" },
                    Products = new[] { "Akıllı Telefon", "Laptop", "Tablet", "Kablosuz Kulaklık", "Akıllı Saat", "Bluetooth Hoparlör", "Powerbank", "Şarj Aleti", "Telefon Kılıfı", "Ekran Koruyucu", "Mouse", "Klavye", "Monitor", "Webcam", "USB Bellek", "SSD Disk", "Gaming Kulaklık", "Drone" },
                    Adjectives = new[] { "Pro", "Max", "Ultra", "Plus", "Lite", "Mini", "Air", "SE", "GT", "Neo" },
                    Descriptions = new[] {
                        "Yüksek performanslı işlemci ve uzun pil ömrü ile günlük kullanım için ideal.",
                        "Şık tasarımı ve güçlü özellikleri ile teknoloji tutkunları için.",
                        "Gelişmiş kamera sistemi ve hızlı şarj özelliği mevcut.",
                        "Ergonomik tasarım ve üstün ses kalitesi sunar.",
                        "Kompakt boyutu ile taşıması kolay, performanstan ödün vermez.",
                        "Yeni nesil teknoloji ile donatılmış premium ürün.",
                        "Oyun ve multimedya için optimize edilmiş performans.",
                        "İnce ve hafif tasarımı ile her yere taşıyabilirsiniz."
                    }
                },
                ["giyim"] = new ProductTemplates
                {
                    Brands = new[] { "Zara", "H&M", "Mango", "Pull&Bear", "Bershka", "Koton", "LC Waikiki", "DeFacto", "Mavi", "Colin's", "LTB", "Nike", "Adidas", "Puma", "Tommy Hilfiger", "Calvin Klein", "Lacoste", "US Polo" },
                    Products = new[] { "T-Shirt", "Gömlek", "Pantolon", "Jean", "Ceket", "Mont", "Kazak", "Hırka", "Sweatshirt", "Elbise", "Etek", "Şort", "Yelek", "Trençkot", "Parka", "Blazer", "Polo Yaka", "Crop Top" },
                    Adjectives = new[] { "Basic", "Slim Fit", "Regular Fit", "Oversize", "Crop", "Vintage", "Premium", "Comfort", "Klasik", "Modern" },
                    Descriptions = new[] {
                        "Yumuşak pamuklu kumaşı ile gün boyu konfor sağlar.",
                        "Şık ve modern tasarımı ile her ortama uygun.",
                        "Kaliteli dikişleri ve dayanıklı kumaşı ile uzun ömürlü.",
                        "Rahat kesimi ile hareket özgürlüğü sunar.",
                        "Trend renkler ve desenlerle kombinlerinizi tamamlayın.",
                        "Mevsimlik koleksiyon, sınırlı sayıda üretim.",
                        "Doğal kumaşlardan üretilmiş, cildinize dost.",
                        "Kolay ütülenebilir ve yıkanabilir pratik parça."
                    }
                },
                ["ayakkabı"] = new ProductTemplates
                {
                    Brands = new[] { "Nike", "Adidas", "Puma", "New Balance", "Converse", "Vans", "Skechers", "Reebok", "Under Armour", "Fila", "Asics", "Hoka", "Salomon", "Timberland", "Dr. Martens", "Clarks" },
                    Products = new[] { "Spor Ayakkabı", "Koşu Ayakkabısı", "Sneaker", "Bot", "Günlük Ayakkabı", "Sandalet", "Terlik", "Loafer", "Oxford", "Chelsea Bot", "Basketbol Ayakkabısı", "Yürüyüş Ayakkabısı", "Trekking Bot", "Mokasen" },
                    Adjectives = new[] { "Air", "Boost", "Gel", "Fresh Foam", "Cloud", "React", "Zoom", "Ultra", "Pro", "Elite" },
                    Descriptions = new[] {
                        "Hafif ve esnek tabanı ile maksimum konfor.",
                        "Nefes alabilen üst malzemesi ile ayaklarınız ferah kalır.",
                        "Şok emici taban teknolojisi ile eklemlerinizi korur.",
                        "Kaymaz taban yapısı ile her zeminde güvenli.",
                        "Ergonomik tasarımı ile uzun yürüyüşlerde bile rahat.",
                        "Premium malzemelerden üretilmiş dayanıklı yapı.",
                        "Modern tasarımı ile stilinizi tamamlayın.",
                        "Geniş kalıp seçenekleri mevcut, her ayağa uygun."
                    }
                },
                ["kozmetik"] = new ProductTemplates
                {
                    Brands = new[] { "L'Oreal", "Maybelline", "MAC", "NYX", "Flormar", "Golden Rose", "Farmasi", "Avon", "Oriflame", "Nivea", "Garnier", "Neutrogena", "La Roche-Posay", "Bioderma", "Estee Lauder", "Clinique", "Kiehl's", "The Ordinary" },
                    Products = new[] { "Ruj", "Fondöten", "Maskara", "Allık", "Far Paleti", "Kaş Kalemi", "Eyeliner", "Pudra", "Primer", "Nemlendirici", "Serum", "Temizleyici", "Tonik", "Güneş Kremi", "Parfüm", "Deodorant", "Saç Spreyi", "Saç Maskesi" },
                    Adjectives = new[] { "Mat", "Parlak", "Kalıcı", "Doğal", "Yoğun", "Hafif", "Su Geçirmez", "Besleyici", "Anti-Aging", "Vitamin C'li" },
                    Descriptions = new[] {
                        "Uzun süre kalıcı formülü ile gün boyu kusursuz görünüm.",
                        "Doğal içeriklerle zenginleştirilmiş cilt dostu formül.",
                        "Hafif dokusu ile cildinize ağırlık yapmaz.",
                        "Yoğun pigmentli, tek sürüşte tam kaplama.",
                        "Dermatolojik olarak test edilmiş, hassas ciltler için uygun.",
                        "Vitaminlerle zenginleştirilmiş bakım formülü.",
                        "Profesyonel sonuçlar için geliştirilmiş özel formül.",
                        "Vegan ve cruelty-free sertifikalı ürün."
                    }
                },
                ["kitap"] = new ProductTemplates
                {
                    Brands = new[] { "Can Yayınları", "İş Bankası Kültür", "Yapı Kredi", "Doğan Kitap", "Epsilon", "Pegasus", "Alfa", "İthaki", "Koridor", "April", "Domingo", "Destek Yayınları", "Timaş", "Hayykitap", "Kronik Kitap" },
                    Products = new[] { "Roman", "Öykü", "Şiir", "Kişisel Gelişim", "Psikoloji", "Felsefe", "Tarih", "Bilim", "Biyografi", "Çocuk Kitabı", "Gençlik Romanı", "Polisiye", "Fantastik", "Bilim Kurgu", "Klasik", "Deneme" },
                    Adjectives = new[] { "Bestseller", "Ödüllü", "Çok Satan", "Yeni Baskı", "Özel Kapak", "Ciltli", "Karton Kapak", "Limited Edition" },
                    Descriptions = new[] {
                        "Dünya genelinde milyonlarca okura ulaşmış başyapıt.",
                        "Ödüllü yazarın kaleminden etkileyici bir hikaye.",
                        "Sayfaları çevirdikçe bırakamayacağınız bir kitap.",
                        "Hayata bakış açınızı değiştirecek önemli bir eser.",
                        "Akıcı üslubu ve derin karakterleriyle unutulmaz.",
                        "Merakla beklenen yeni çeviriyle Türkçede.",
                        "Her yaştan okur için keyifli bir okuma deneyimi.",
                        "Eleştirmenlerin övgüyle karşıladığı çağdaş klasik."
                    }
                },
                ["aksesuar"] = new ProductTemplates
                {
                    Brands = new[] { "Michael Kors", "Guess", "Fossil", "Daniel Wellington", "Tommy Hilfiger", "Calvin Klein", "Swarovski", "Pandora", "Ray-Ban", "Oakley", "Samsonite", "American Tourister", "Kipling", "Herschel", "Fjällräven" },
                    Products = new[] { "Kol Saati", "Güneş Gözlüğü", "Çanta", "Sırt Çantası", "Cüzdan", "Kemer", "Şapka", "Bere", "Atkı", "Eldiven", "Kolye", "Bileklik", "Küpe", "Yüzük", "Anahtarlık", "Kartlık" },
                    Adjectives = new[] { "Klasik", "Modern", "Minimalist", "Vintage", "Elegant", "Sportif", "Casual", "Premium", "Lüks" },
                    Descriptions = new[] {
                        "Şık tasarımı ile her kombine uyum sağlar.",
                        "Kaliteli malzemelerden özenle üretilmiştir.",
                        "Zamansız tasarımı ile yıllarca kullanabilirsiniz.",
                        "Hediye için ideal, özel kutusunda sunulur.",
                        "Pratik bölmeleri ile düzenli taşıma imkanı.",
                        "UV korumalı camları ile gözlerinizi korur.",
                        "Su geçirmez özelliği ile her hava koşuluna uygun.",
                        "Ayarlanabilir tasarımı ile kişiselleştirilebilir."
                    }
                },
                ["ev & yaşam"] = new ProductTemplates
                {
                    Brands = new[] { "IKEA", "English Home", "Madame Coco", "Karaca", "Bernardo", "Emsan", "Taç", "Özdilek", "Linens", "Hobby", "Kütahya Porselen", "Korkmaz", "Arzum", "Philips", "Dyson" },
                    Products = new[] { "Yastık", "Yorgan", "Nevresim", "Havlu", "Perde", "Halı", "Aydınlatma", "Vazo", "Çerçeve", "Mum", "Saksı", "Tencere Seti", "Tava", "Bardak Seti", "Tabak Seti", "Kahve Makinesi" },
                    Adjectives = new[] { "Organik", "Pamuklu", "Kadife", "Modern", "Rustik", "Bohem", "Minimal", "Klasik", "Lüks" },
                    Descriptions = new[] {
                        "Evinize sıcak ve şık bir dokunuş katın.",
                        "Yüksek kaliteli malzemelerden üretilmiştir.",
                        "Kolay temizlenebilir ve uzun ömürlü.",
                        "Modern tasarımı ile dekorasyonunuzu tamamlayın.",
                        "Pratik kullanım için tasarlanmış fonksiyonel ürün.",
                        "Doğal malzemelerden üretilmiş çevre dostu.",
                        "Zarif detayları ile fark yaratan tasarım.",
                        "Set halinde ekonomik ve şık."
                    }
                },
                ["spor"] = new ProductTemplates
                {
                    Brands = new[] { "Nike", "Adidas", "Puma", "Under Armour", "Reebok", "Decathlon", "Domyos", "Kipsta", "Nabaiji", "Quechua", "Wilson", "Head", "Speedo", "Arena", "Garmin", "Polar" },
                    Products = new[] { "Spor Tayt", "Spor Sütyeni", "Forma", "Şort", "Eşofman", "Spor Çanta", "Yoga Matı", "Dambıl Seti", "Direnç Bandı", "Atlama İpi", "Futbol Topu", "Basketbol Topu", "Tenis Raketi", "Yüzme Gözlüğü", "Bisiklet Kaskı", "Spor Saati" },
                    Adjectives = new[] { "Profesyonel", "Training", "Performance", "Compression", "Dry-Fit", "Climate", "Thermal", "Aerobic" },
                    Descriptions = new[] {
                        "Yüksek performanslı kumaşı ile maksimum konfor.",
                        "Nefes alabilen yapısı ile ter tutmaz.",
                        "Esnek yapısı ile sınırsız hareket özgürlüğü.",
                        "Profesyonel sporcular için tasarlanmış kalite.",
                        "Dayanıklı yapısı ile uzun süreli kullanım.",
                        "Ergonomik tasarımı ile sakatlıkları önler.",
                        "Hafif yapısı ile performansınızı artırır.",
                        "Antibakteriyel özelliği ile hijyenik kullanım."
                    }
                }
            };
        }

        private (string name, string description) GetProductNameAndDescription(
            string categoryName, 
            Dictionary<string, ProductTemplates> productData, 
            Random random,
            int index,
            List<string> allTemplateKeys)
        {
            // Kategoriye göre template bul
            ProductTemplates? template = null;
            
            // Önce tam eşleşme dene
            foreach (var key in productData.Keys)
            {
                if (categoryName.Contains(key) || key.Contains(categoryName))
                {
                    template = productData[key];
                    break;
                }
            }

            // Eğer kategori bulunamazsa rastgele bir template seç
            if (template == null)
            {
                var randomKey = allTemplateKeys[random.Next(allTemplateKeys.Count)];
                template = productData[randomKey];
            }

            var brand = template.Brands[random.Next(template.Brands.Length)];
            var product = template.Products[random.Next(template.Products.Length)];
            var adjective = template.Adjectives[random.Next(template.Adjectives.Length)];
            var description = template.Descriptions[random.Next(template.Descriptions.Length)];

            // %50 ihtimalle adjective ekle
            var name = random.Next(2) == 0 
                ? $"{brand} {adjective} {product}" 
                : $"{brand} {product}";

            return (name, description);
        }

        private class ProductTemplates
        {
            public string[] Brands { get; set; } = Array.Empty<string>();
            public string[] Products { get; set; } = Array.Empty<string>();
            public string[] Adjectives { get; set; } = Array.Empty<string>();
            public string[] Descriptions { get; set; } = Array.Empty<string>();
        }

        /// <summary>
        /// 🔥 Şablonlardaki kategoriye uygun resimleri kullanır
        /// Her ürün için farklı bir resim seçer
        /// </summary>
        private string GetDynamicUnsplashImage(string categoryName, int index, Random random, List<ProductTemplate> templates)
        {
            if (templates == null || !templates.Any())
                return "https://via.placeholder.com/400";
            
            // Tüm şablonlardaki resimleri topla
            var allImages = new List<string>();
            foreach (var template in templates)
            {
                if (template.Images != null && template.Images.Length > 0)
                {
                    allImages.AddRange(template.Images);
                }
            }
            
            if (!allImages.Any())
                return "https://via.placeholder.com/400";
            
            // Her ürün için benzersiz bir index ile resim seç
            var imageIndex = (index * 7919 + random.Next(10000) + (int)(DateTime.Now.Ticks % 1000)) % allImages.Count;
            return allImages[imageIndex];
        }

        /// <summary>
        /// 🔥 Kategoriye göre dinamik Unsplash resmi döndürür (ESKİ - KULLANILMIYOR)
        /// </summary>
        private string GetDynamicUnsplashImageOld(string categoryName, int index, Random random)
        {
            var category = categoryName.ToLower().Trim();
            
            // Kategoriye göre Unsplash photo ID'leri (her kategori için 100+ farklı resim)
            // Her ID benzersiz bir Unsplash fotoğrafına işaret eder
            var photoIds = category switch
            {
                var c when c.Contains("elektronik") => new[] { 
                    "1592750475338-74b7b21085ab", "1510557880182-3d4d3cba35a5", "1610945265064-0e34e5519bbf",
                    "1511707171634-5f897ff02aa9", "1517336714731-489689fd1ca8", "1541807084-5c52b6b3adef",
                    "1593642632559-0c6d3fc62b89", "1588872657578-7efd1f1555ed", "1544244015-0df4b3ffc6b0",
                    "1585790050230-5dd28404ccb9", "1505740420928-5e560c06d30e", "1583394838336-acd977736f90",
                    "1600294037681-c80b4cb5b434", "1588423771073-b8903fbb85b5", "1546868871-7041f2a55e12",
                    "1579586337278-3befd40fd17a", "1593359677879-a4bb92f829d1", "1461151304267-38535e780c79",
                    "1608043152269-423dbba4e7e1", "1545454675-3531b543be5d", "1516035069371-29a1b244cc32",
                    "1502920917128-1aa500764cbd", "1523271234650-1cecd4b5b179", "1504639725180-4188a3bd5632",
                    "1512941939419-2985b27682e7", "1526738548639-51f7f31507b3", "1504707748692-7c4e2b3b3b3b",
                    "1496181133206-80ce9b88a853", "1484704848000-03ed27a3cc14", "1511707171634-5f897ff02aa9",
                    "1505740420928-5e560c06d30e", "1512941939419-2985b27682e7", "1526738548639-51f7f31507b3",
                    "1504707748692-7c4e2b3b3b3b", "1496181133206-80ce9b88a853", "1484704848000-03ed27a3cc14"
                },
                var c when c.Contains("giyim") => new[] { 
                    "1521572163474-6864f9cf17ab", "1583743814966-8936f5b7be1a", "1596755094514-f87e34085b2c",
                    "1602810318383-e386cc2a3ccf", "1542272604-787c3835535d", "1541099649105-f69ad21f3246",
                    "1624378439575-d8705ad7ae80", "1552902865-b72c031ac5ea", "1595777457583-95e059d581b8",
                    "1572804013309-59a88b7e92f1", "1594938298603-c8148c4dae35", "1507003211169-0a1dd7228f2d",
                    "1551028719-00167b16eac5", "1434389677669-e08b4cac3105", "1576566588028-4147f3842f27",
                    "1544923246-77307dd628b5", "1591047139829-d91aecb6caea", "1556821840-3a63f95609a7",
                    "1578681994506-b8f463449011", "1521572163474-6864f9cf17ab", "1583743814966-8936f5b7be1a",
                    "1596755094514-f87e34085b2c", "1602810318383-e386cc2a3ccf", "1542272604-787c3835535d",
                    "1541099649105-f69ad21f3246", "1624378439575-d8705ad7ae80", "1552902865-b72c031ac5ea",
                    "1595777457583-95e059d581b8", "1572804013309-59a88b7e92f1", "1594938298603-c8148c4dae35",
                    "1507003211169-0a1dd7228f2d", "1551028719-00167b16eac5", "1434389677669-e08b4cac3105",
                    "1576566588028-4147f3842f27", "1544923246-77307dd628b5", "1591047139829-d91aecb6caea",
                    "1556821840-3a63f95609a7", "1578681994506-b8f463449011"
                },
                var c when c.Contains("ayakkab") => new[] { 
                    "1542291026-7eec264c27ff", "1460353581641-37baddab0fa2", "1556906781-9a412961c28c",
                    "1595950653106-6c9ebd614d3a", "1608231387042-66d1773070a5", "1600185365483-26d7a4cc7519",
                    "1539185441755-769473a23570", "1551107696-a4b0c5a0d9a2", "1562183241-b937e95585b6",
                    "1603808033192-082d6919d3e1", "1605348532760-6753d2c43329", "1584735175315-9d5df23860e6",
                    "1520639888713-7851133b1ed0", "1605812860427-4024433a70fd", "1608256246200-53e635b5b65f",
                    "1605733160314-4fc7dac4bb16", "1607522370275-f14206abe5d3", "1494496195158-c3becb4f2475",
                    "1525966222134-fcfa99b8ae77", "1606107557195-0e29a4b5b4aa", "1603487742131-4160ec999306"
                },
                var c when c.Contains("kozmetik") => new[] { 
                    "1541643600914-78b084683601", "1523293182086-7651a899d37f", "1594035910387-fea47794261f",
                    "1590736969955-71cc94901144", "1586495777744-4413f21062fa", "1571781926291-c477ebfd024b",
                    "1596462502278-27bfdc403348", "1631730486572-226d1f595b68", "1512496015851-a90fb38ba796",
                    "1631214524020-7e18db9a8f92", "1620916566398-39f1143ab7be", "1608248597279-f99d160bfcbc",
                    "1617897903246-719242758050", "1611930022073-b7a4ba5fcccd", "1556228720-195a672e8a03",
                    "1535585209827-a15fcdbc4c2d", "1626806787461-102c1bfaaea1", "1583241800698-e8ab01830a07"
                },
                var c when c.Contains("çanta") || c.Contains("canta") => new[] { 
                    "1584917865442-de89df76afd3", "1590874103328-eac38a683ce7", "1566150905458-1bf1fc113f0d",
                    "1594223274512-ad4803739b7c", "1559563458-527698bf5295", "1553062407-98eeb64c6a62",
                    "1581605405669-fcdf81165afa", "1548036328-c9fa89d128fa", "1591561954557-26941169b49e",
                    "1627123424574-724758594e93", "1606503825008-909a67e63c3d", "1565026057447-bc90a3dceb87",
                    "1581553680321-4fffae59fccd", "1585916420730-d7f95e942d43"
                },
                var c when c.Contains("kitap") => new[] { 
                    "1544947950-fa07a98d237f", "1512820790803-83ca734da794", "1543002588-bfa74002ed7e",
                    "1476275466078-4007374efbbe", "1495446815901-a7297e633e8d", "1474366521946-c3d4b507abf2",
                    "1618666012174-83b441c0bc76", "1497633762265-9d179a990aa6"
                },
                var c when c.Contains("aksesuar") => new[] { 
                    "1546868871-7041f2a55e12", "1579586337278-3befd40fd17a", "1521572163474-6864f9cf17ab",
                    "1583743814966-8936f5b7be1a", "1627123424574-724758594e93", "1606503825008-909a67e63c3d"
                },
                var c when c.Contains("ev") || c.Contains("yaşam") => new[] { 
                    "1586023493175-9c6715d9f081", "1556911220-bb31a88c6833", "1560448204-e02f714c7360"
                },
                var c when c.Contains("spor") => new[] { 
                    "1571019613454-1cb2f99b2d8b", "1544367567-cc0c1c5e5c0", "1576678927484-b2995a0f54da"
                },
                _ => new[] { 
                    "1592750475338-74b7b21085ab", "1510557880182-3d4d3cba35a5", "1521572163474-6864f9cf17ab"
                }
            };
            
            // Her ürün için TAMAMEN BENZERSİZ bir seed oluştur
            var categoryHash = Math.Abs(category.GetHashCode());
            var randomValue = random.Next(1000000);
            var timestamp = DateTime.Now.Ticks % 1000000;
            
            // Benzersiz seed: Her ürün için farklı bir sayı (çoklu kombinasyon)
            var uniqueSeed = Math.Abs(
                (index * 7919L) + 
                (randomValue * 17L) + 
                (categoryHash * 31L) + 
                (timestamp * 7L) +
                (index * randomValue * 13L) + // Çapraz kombinasyon
                (categoryHash * randomValue * 19L) // Daha fazla çeşitlilik
            );
            
            // Kategoriye göre farklı seed aralıkları (her kategori için farklı resimler)
            var categorySeedBase = category switch
            {
                var c when c.Contains("elektronik") => 100000L,
                var c when c.Contains("giyim") => 200000L,
                var c when c.Contains("ayakkab") => 300000L,
                var c when c.Contains("kozmetik") => 400000L,
                var c when c.Contains("çanta") || c.Contains("canta") => 500000L,
                var c when c.Contains("kitap") => 600000L,
                _ => 0L
            };
            
            // Final seed: Kategori base + benzersiz seed
            var finalSeed = categorySeedBase + (uniqueSeed % 100000L);
            
            // Picsum Photos: Her seed için farklı resim döner
            // Kategoriye göre farklı seed'ler = Kategoriye uygun farklı resimler
            return $"https://picsum.photos/seed/{finalSeed}/400/400";
        }

        /// <summary>
        /// Kategoriye göre uygun resim URL'si döndürür (Eski metod - loremflickr)
        /// </summary>
        private string GetCategoryImageUrl(string categoryName, int index)
        {
            var category = categoryName.ToLower().Trim();
            
            // Kategori bazlı anahtar kelimeler
            var imageKeywords = category switch
            {
                var c when c.Contains("elektronik") => "laptop,smartphone,electronics,technology",
                var c when c.Contains("giyim") => "fashion,clothing,tshirt,dress",
                var c when c.Contains("ayakkab") => "shoes,sneakers,boots,footwear",
                var c when c.Contains("kozmetik") => "cosmetics,makeup,beauty,skincare",
                var c when c.Contains("çanta") || c.Contains("canta") => "bag,handbag,backpack,purse",
                var c when c.Contains("kitap") => "book,library,reading,novel",
                var c when c.Contains("aksesuar") => "watch,jewelry,accessories,sunglasses",
                var c when c.Contains("ev") || c.Contains("yaşam") => "furniture,home,decor,interior",
                var c when c.Contains("spor") => "sports,fitness,gym,workout",
                _ => "product,shopping,store"
            };

            // loremflickr.com ile kategori bazlı resimler
            // lock parametresi ile her ürün için farklı ama tutarlı resim
            return $"https://loremflickr.com/400/400/{imageKeywords}?lock={index}";
        }


        /// <summary>
        /// 🔥 DummyJSON API'den direkt veritabanına ekleme (CSV olmadan)
        /// GET: api/Seed/from-dummyjson-direct?count=1000
        /// </summary>
        [HttpGet("from-dummyjson-direct")]
        public async Task<IActionResult> SeedFromDummyJsonDirect([FromQuery] int count = 1000)
        {
            try
            {
                // Kategorileri al
                var categories = await _context.Categories.ToListAsync();
                var categoryDict = categories.ToDictionary(c => c.Name?.ToLower().Trim() ?? "", c => c);

                // Kategori eşleştirme haritası
                var categoryMapping = new Dictionary<string, string>
                {
                    { "smartphones", "Elektronik" },
                    { "laptops", "Elektronik" },
                    { "fragrances", "Kozmetik" },
                    { "skincare", "Kozmetik" },
                    { "tops", "Giyim" },
                    { "womens-dresses", "Giyim" },
                    { "womens-shoes", "Ayakkabı" },
                    { "mens-shirts", "Giyim" },
                    { "mens-shoes", "Ayakkabı" },
                    { "mens-watches", "Giyim" },
                    { "womens-watches", "Giyim" },
                    { "womens-bags", "Çanta" },
                    { "womens-jewellery", "Kozmetik" },
                    { "sunglasses", "Giyim" },
                    { "automotive", "Elektronik" },
                    { "motorcycle", "Elektronik" },
                    { "lighting", "Elektronik" },
                    { "groceries", "Kitap" },
                    { "home-decoration", "Giyim" },
                    { "furniture", "Giyim" }
                };

                var allProducts = new List<JsonElement>();
                var limit = 100;
                var skip = 0;
                var totalFetched = 0;
                var maxSkip = 2000; // DummyJSON'da toplam ~2000 ürün var

                // DummyJSON API'den ürünleri çek (farklı sayfalardan)
                while (totalFetched < count && skip < maxSkip)
                {
                    var url = $"https://dummyjson.com/products?limit={Math.Min(limit, count - totalFetched)}&skip={skip}";
                    var response = await _httpClient.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                        break;

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("products", out var productsArray))
                    {
                        foreach (var product in productsArray.EnumerateArray())
                        {
                            allProducts.Add(product);
                            totalFetched++;
                            if (totalFetched >= count) break;
                        }
                    }

                    if (totalFetched >= count) break;
                    skip += limit;
                    await Task.Delay(200);
                }

                // Eğer yeterli ürün yoksa, Fake Store API'den de çek
                if (totalFetched < count)
                {
                    try
                    {
                        var fakeStoreResponse = await _httpClient.GetAsync("https://fakestoreapi.com/products");
                        if (fakeStoreResponse.IsSuccessStatusCode)
                        {
                            var fakeStoreJson = await fakeStoreResponse.Content.ReadAsStringAsync();
                            var fakeStoreData = JsonSerializer.Deserialize<JsonElement>(fakeStoreJson);
                            
                            if (fakeStoreData.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var product in fakeStoreData.EnumerateArray())
                                {
                                    if (totalFetched >= count) break;
                                    allProducts.Add(product);
                                    totalFetched++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fake Store API hatası olursa devam et
                    }
                }

                var productsToAdd = new List<Product>();
                var addedCount = 0;
                var skippedCount = 0;
                var random = new Random(); // Resim seçimi için

                foreach (var product in allProducts)
                {
                    try
                    {
                        // DummyJSON: "title", Fake Store: "name"
                        var name = product.TryGetProperty("title", out var titleProp) 
                            ? titleProp.GetString() ?? "" 
                            : (product.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "");
                        
                        var description = product.TryGetProperty("description", out var descProp) 
                            ? descProp.GetString() ?? "" 
                            : "";
                        
                        var price = product.TryGetProperty("price", out var priceProp) 
                            ? priceProp.GetDecimal() 
                            : 0;
                        
                        // DummyJSON: "stock", Fake Store: yok (varsayılan 100)
                        var stock = product.TryGetProperty("stock", out var stockProp) 
                            ? stockProp.GetInt32() 
                            : random.Next(10, 200);
                        
                        var category = product.TryGetProperty("category", out var catProp) 
                            ? catProp.GetString()?.ToLower() ?? "" 
                            : "";
                        
                        // Resim URL'si - Her ürün için farklı resim seç
                        string imageUrl = "https://via.placeholder.com/400";
                        
                        // DummyJSON formatı
                        if (product.TryGetProperty("images", out JsonElement imagesArray) && imagesArray.GetArrayLength() > 0)
                        {
                            // Images array'inden rastgele bir resim seç
                            var imageIndex = random.Next(imagesArray.GetArrayLength());
                            imageUrl = imagesArray[imageIndex].GetString() ?? imageUrl;
                            
                            // Cache bypass için benzersiz parametre ekle (her ürün için farklı resim)
                            var uniqueParam = $"v={addedCount}_{random.Next(10000)}";
                            imageUrl += (imageUrl.Contains("?") ? "&" : "?") + uniqueParam;
                        }
                        else if (product.TryGetProperty("thumbnail", out JsonElement thumbnail))
                        {
                            imageUrl = thumbnail.GetString() ?? imageUrl;
                            var uniqueParam = $"v={addedCount}_{random.Next(10000)}";
                            imageUrl += (imageUrl.Contains("?") ? "&" : "?") + uniqueParam;
                        }
                        // Fake Store API formatı
                        else if (product.TryGetProperty("image", out JsonElement fakeStoreImage))
                        {
                            imageUrl = fakeStoreImage.GetString() ?? imageUrl;
                            var uniqueParam = $"v={addedCount}_{random.Next(10000)}";
                            imageUrl += (imageUrl.Contains("?") ? "&" : "?") + uniqueParam;
                        }

                        // Kategori eşleştirme
                        var mappedCategoryName = categoryMapping.ContainsKey(category) 
                            ? categoryMapping[category] 
                            : "Elektronik"; // Varsayılan

                        var categoryKey = mappedCategoryName.ToLower().Trim();
                        if (!categoryDict.ContainsKey(categoryKey))
                        {
                            skippedCount++;
                            continue;
                        }

                        var targetCategory = categoryDict[categoryKey];

                        var dbProduct = new Product
                        {
                            Name = name,
                            Description = description,
                            Price = price,
                            ImageUrl = imageUrl,
                            CategoryId = targetCategory.Id,
                            SubCategoryId = null,
                            Stock = stock,
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(dbProduct);
                        addedCount++;
                    }
                    catch
                    {
                        skippedCount++;
                        continue;
                    }
                }

                if (productsToAdd.Any())
                {
                    await _context.Products.AddRangeAsync(productsToAdd);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = $"✅ DummyJSON'dan {addedCount} ürün eklendi!",
                    added = addedCount,
                    skipped = skippedCount,
                    totalProducts = await _context.Products.CountAsync()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// 🔥 Unsplash API'den kategori bazlı ürünler ekler
        /// GET: api/Seed/from-unsplash?countPerCategory=200
        /// Her kategori için Unsplash'tan ilgili fotoğraflar çeker
        /// </summary>
        [HttpGet("from-unsplash")]
        public async Task<IActionResult> SeedFromUnsplash([FromQuery] int countPerCategory = 200)
        {
            try
            {
                var accessKey = _configuration["Unsplash:AccessKey"];
                if (string.IsNullOrEmpty(accessKey) || accessKey == "YOUR_UNSPLASH_ACCESS_KEY_HERE")
                {
                    return BadRequest(new { 
                        error = "Unsplash Access Key bulunamadı!",
                        message = "appsettings.json dosyasına 'Unsplash:AccessKey' ekleyin. Key almak için: https://unsplash.com/developers"
                    });
                }

                var categories = await _context.Categories.ToListAsync();
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                // Debug: Kategori isimlerini logla
                var categoryNames = categories.Select(c => c.Name ?? "NULL").ToList();

                // Kategori -> Unsplash Photo ID listesi (kendi seçtiğin fotoğraflar)
                // Unsplash'ta bir fotoğrafın ID'sini bulmak için: https://unsplash.com/photos/{photo-id} URL'sindeki ID'yi al
                var categoryPhotoIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Elektronik", new List<string>
                        {
                            // Örnek ID'ler - kendi seçtiğin ID'leri buraya ekle
                            // "abc123", "def456", "ghi789" gibi
                        }
                    },
                    { "Giyim", new List<string>() },
                    { "Ayakkabı", new List<string>() },
                    { "Kozmetik", new List<string>() },
                    { "Çanta", new List<string>() },
                    { "Kitap", new List<string>() }
                };

                // Kategori -> Unsplash search query eşleştirmesi (ID yoksa arama yapmak için)
                var categoryQueries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Elektronik", "iPhone Samsung laptop computer tablet smartphone iPad MacBook Dell HP Lenovo Asus gaming phone wireless earbuds smartwatch" },
                    { "Giyim", "fashion clothing t-shirt jeans jacket hoodie sweater dress shirt polo shirt casual wear streetwear" },
                    { "Ayakkabı", "Nike Adidas sneakers running shoes boots high heels sandals sport shoes casual shoes" },
                    { "Kozmetik", "makeup lipstick foundation mascara eyeshadow blush skincare cream serum face mask beauty products" },
                    { "Çanta", "handbag backpack purse tote bag crossbody bag leather bag designer bag travel bag" },
                    { "Kitap", "book reading novel literature fiction non-fiction hardcover paperback library" }
                };

                var productsToAdd = new List<Product>();
                var random = new Random();
                var categoryStats = new Dictionary<string, int>();

                foreach (var category in categories)
                {
                    var catName = category.Name ?? "";
                    var searchQuery = categoryQueries.ContainsKey(catName) 
                        ? categoryQueries[catName] 
                        : catName.ToLower();

                    // Her kategori için ürün şablonları
                    var productTemplates = GetCategoryProducts(catName.ToLower());
                    if (!productTemplates.Any())
                        continue;

                    // Fotoğraf bilgilerini sakla (URL + açıklama)
                    var photoData = new List<(string url, string description, string altDescription)>();

                    // ÖNCE: Kullanıcının seçtiği ID'lerden fotoğraf çek (döngüsel kullanım)
                    if (categoryPhotoIds.ContainsKey(catName) && categoryPhotoIds[catName].Any())
                    {
                        var photoIds = categoryPhotoIds[catName];
                        var photoIndex = 0;

                        while (photoData.Count < countPerCategory)
                        {
                            var photoId = photoIds[photoIndex % photoIds.Count]; // Döngüsel kullanım
                            photoIndex++;

                            try
                            {
                                var photoUrl = $"https://api.unsplash.com/photos/{photoId}?client_id={accessKey}";
                                var response = await _httpClient.GetAsync(photoUrl);
                                
                                if (response.IsSuccessStatusCode)
                                {
                                    var json = await response.Content.ReadAsStringAsync();
                                    var photo = JsonSerializer.Deserialize<JsonElement>(json);

                                    if (photo.TryGetProperty("urls", out var urls) && urls.TryGetProperty("regular", out var regularUrl))
                                    {
                                        var imageUrl = regularUrl.GetString();
                                        if (!string.IsNullOrEmpty(imageUrl))
                                        {
                                            var description = photo.TryGetProperty("description", out var descProp) 
                                                ? descProp.GetString() ?? "" 
                                                : "";
                                            
                                            var altDescription = photo.TryGetProperty("alt_description", out var altDescProp) 
                                                ? altDescProp.GetString() ?? "" 
                                                : "";

                                            photoData.Add((imageUrl, description, altDescription));
                                        }
                                    }
                                }

                                await Task.Delay(100); // Rate limiting
                            }
                            catch
                            {
                                // ID geçersizse atla, döngü devam etsin
                                continue;
                            }
                        }
                    }

                    // EĞER: ID'ler yeterli değilse, arama yap
                    var page = 1;
                    // maxPages: countPerCategory'ye göre dinamik hesapla (her sayfada 30 fotoğraf)
                    // Örnek: 200 ürün istiyorsan → 200/30 = 7 sayfa (yuvarlanır 8)
                    var maxPages = (int)Math.Ceiling((double)countPerCategory / 30.0) + 2; // +2 ekstra sayfa (çeşitlilik için)
                    // Maksimum 50 sayfa sınırı (1500 fotoğraf) - Unsplash rate limit için güvenli
                    maxPages = Math.Min(maxPages, 50);

                    while (photoData.Count < countPerCategory && page <= maxPages)
                    {
                        try
                        {
                            var unsplashUrl = $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(searchQuery)}&client_id={accessKey}&per_page=30&page={page}&orientation=portrait";
                            var response = await _httpClient.GetAsync(unsplashUrl);
                            
                            if (!response.IsSuccessStatusCode)
                            {
                                // API hatası varsa logla ve dur
                                var errorContent = await response.Content.ReadAsStringAsync();
                                break;
                            }

                            var json = await response.Content.ReadAsStringAsync();
                            var data = JsonSerializer.Deserialize<JsonElement>(json);

                            if (data.TryGetProperty("results", out var resultsArray))
                            {
                                foreach (var photo in resultsArray.EnumerateArray())
                                {
                                    if (photo.TryGetProperty("urls", out var urls))
                                    {
                                        // Regular size kullan
                                        if (urls.TryGetProperty("regular", out var regularUrl))
                                        {
                                            var photoUrl = regularUrl.GetString();
                                            if (string.IsNullOrEmpty(photoUrl))
                                                continue;

                                            // Fotoğraf açıklamalarını al
                                            var description = photo.TryGetProperty("description", out var descProp) 
                                                ? descProp.GetString() ?? "" 
                                                : "";
                                            
                                            var altDescription = photo.TryGetProperty("alt_description", out var altDescProp) 
                                                ? altDescProp.GetString() ?? "" 
                                                : "";

                                            photoData.Add((photoUrl, description, altDescription));
                                        }
                                    }
                                }
                            }

                            page++;
                            await Task.Delay(100); // Rate limiting için
                        }
                        catch
                        {
                            break;
                        }
                    }

                    if (!photoData.Any())
                    {
                        // Fotoğraf bulunamadı - kategori atlanıyor
                        categoryStats[catName] = 0;
                        continue;
                    }

                    // Ürünleri oluştur (fotoğraf açıklamalarına göre)
                    for (int i = 0; i < countPerCategory && i < photoData.Count; i++)
                    {
                        var photo = photoData[i];
                        var imageUrl = photo.url;

                        // Fotoğraf açıklamasından ürün adı ve açıklaması oluştur
                        string productName;
                        string productDescription;

                        if (!string.IsNullOrEmpty(photo.description))
                        {
                            // Açıklamadan ilk birkaç kelimeyi ürün adı yap
                            var words = photo.description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            productName = words.Length > 0 
                                ? string.Join(" ", words.Take(Math.Min(5, words.Length))) 
                                : $"{catName} Ürün {i + 1}";
                            productDescription = photo.description;
                        }
                        else if (!string.IsNullOrEmpty(photo.altDescription))
                        {
                            var words = photo.altDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            productName = words.Length > 0 
                                ? string.Join(" ", words.Take(Math.Min(5, words.Length))) 
                                : $"{catName} Ürün {i + 1}";
                            productDescription = photo.altDescription;
                        }
                        else
                        {
                            // Açıklama yoksa şablondan al
                            var template = productTemplates[random.Next(productTemplates.Count)];
                            productName = AddVariation(template.Name, i, random);
                            productDescription = template.Description;
                        }

                        // Fiyat ve stok (kategoriye göre şablondan)
                        var priceTemplate = productTemplates[random.Next(productTemplates.Count)];
                        var price = Math.Round((decimal)(priceTemplate.MinPrice + random.NextDouble() * (priceTemplate.MaxPrice - priceTemplate.MinPrice)), 2);
                        var stock = random.Next(5, 300);

                        var product = new Product
                        {
                            Name = productName,
                            Description = productDescription,
                            Price = price,
                            ImageUrl = imageUrl,
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            Stock = stock,
                            Status = stock > 0 ? "Stokta var" : "Tükendi"
                        };

                        productsToAdd.Add(product);
                    }

                    categoryStats[catName] = countPerCategory;
                }

                if (productsToAdd.Any())
                {
                    await _context.Products.AddRangeAsync(productsToAdd);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = $"✅ Unsplash'tan {productsToAdd.Count} ürün eklendi!",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    imageSource = "Unsplash API",
                    categoriesFound = categoryNames,
                    categoriesProcessed = categoryStats.Keys.ToList(),
                    categoriesSkipped = categoryNames.Where(c => !categoryStats.ContainsKey(c)).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}

