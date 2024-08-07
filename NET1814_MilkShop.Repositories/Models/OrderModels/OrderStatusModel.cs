﻿using System.ComponentModel.DataAnnotations;

namespace NET1814_MilkShop.Repositories.Models.OrderModels;

public class OrderStatusModel
{
    /// <summary>
    /// Update order status (1: Pending, 2: Processing, 3: Shipped)
    /// </summary>
    [Required(ErrorMessage = "Status Id is required")]
    [Range(1, 3, ErrorMessage = "Status Id must be in range 1-3")]
    public int StatusId { get; set; }
}