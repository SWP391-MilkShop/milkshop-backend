﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NET1814_MilkShop.Repositories.Data.Entities;

[Table("Products")]
public partial class Product
{
    [Key]
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? Quantity { get; set; }

    public decimal? OriginalPrice { get; set; }

    public decimal? SalePrice { get; set; }

    public int? CategoryId { get; set; }
    [Column("brand_id")]
    public int? BrandId { get; set; }

    public int? UnitId { get; set; }
    [Column("status_id")]
    [ForeignKey("ProductStatus")]
    public int? StatusId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Brand? Brand { get; set; }

    public virtual ICollection<CartDetail> CartDetails { get; set; } = new List<CartDetail>();

    public virtual Category? Category { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<ProductAnalytic> ProductAnalytics { get; set; } =
        new List<ProductAnalytic>();

    public virtual ICollection<ProductAttributeValue> ProductAttributeValues { get; set; } =
        new List<ProductAttributeValue>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ProductStatus? ProductStatus { get; set; }
    public virtual Unit? Unit { get; set; }
}
