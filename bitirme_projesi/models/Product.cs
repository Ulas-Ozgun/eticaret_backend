using bitirme_projesi.Models;
using System.ComponentModel.DataAnnotations;

public class Product
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public string ImageUrl { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; }
}
