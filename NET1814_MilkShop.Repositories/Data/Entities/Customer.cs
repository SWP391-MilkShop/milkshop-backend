﻿using NET1814_MilkShop.Repositories.Data.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NET1814_MilkShop.Repositories.Data.Entities;

[Table("customers")]
public partial class Customer : IAuditableEntity
{
    [Key]
    public Guid UserId { get; set; }

    public int Points { get; set; }

    [Column(TypeName = "nvarchar(20)")]
    public string? PhoneNumber { get; set; }

    [Column(TypeName = "nvarchar(255)")]
    public string? Email { get; set; }

    [Column(TypeName = "nvarchar(255)")]
    public string? GoogleId { get; set; }

    [Column(TypeName = "nvarchar(255)")]
    public string? ProfilePictureUrl { get; set; }

    [Column("created_at", TypeName = "datetime2")]
    public DateTime CreatedAt { get; set; }

    [Column("modified_at", TypeName = "datetime2")]
    public DateTime? ModifiedAt { get; set; }

    [Column("deleted_at", TypeName = "datetime2")]
    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = [];

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = [];

    public virtual ICollection<Order> Orders { get; set; } = [];

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = [];
}
