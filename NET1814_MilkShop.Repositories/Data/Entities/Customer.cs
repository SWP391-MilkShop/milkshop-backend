﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NET1814_MilkShop.Repositories.Data.Entities;

[Table("Customers")]
public partial class Customer
{
    [Key]
    public Guid UserId { get; set; }

    public int Points { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? GoogleId { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = [];

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } = [];

    public virtual ICollection<Order> Orders { get; set; } = [];

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = [];
}