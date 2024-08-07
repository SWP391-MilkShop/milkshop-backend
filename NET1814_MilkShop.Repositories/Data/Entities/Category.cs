﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NET1814_MilkShop.Repositories.Data.Interfaces;

namespace NET1814_MilkShop.Repositories.Data.Entities;

[Table("categories")]
public class Category : IAuditableEntity
{
    [Key] public int Id { get; set; }

    [Column("name", TypeName = "nvarchar(255)")]
    public string Name { get; set; } = null!;

    [Column("description", TypeName = "nvarchar(2000)")]
    public string? Description { get; set; }

    [DefaultValue(false)] public bool IsActive { get; set; }

    [ForeignKey("Parent")]
    [Column("parent_id")]
    //Default parent is 0 (root category)
    public int? ParentId { get; set; }

    [Column("created_at", TypeName = "datetime2")]
    public DateTime CreatedAt { get; set; }

    [Column("modified_at", TypeName = "datetime2")]
    public DateTime? ModifiedAt { get; set; }

    [Column("deleted_at", TypeName = "datetime2")]
    public DateTime? DeletedAt { get; set; }

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Category> SubCategories { get; set; } = [];

    public virtual ICollection<Product> Products { get; set; } = [];
}