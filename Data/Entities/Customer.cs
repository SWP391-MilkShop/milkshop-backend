﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SWP391_DEMO.Entities;

[Table("Customers")]
public partial class Customer
{
    [Key]
    public Guid UserId { get; set; }

    public int? Points { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? GoogleId { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; } =
        new List<CustomerAddress>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();
}
