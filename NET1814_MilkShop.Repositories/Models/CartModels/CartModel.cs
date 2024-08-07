﻿namespace NET1814_MilkShop.Repositories.Models.CartModels;

public class CartModel
{
    public int Id { get; set; }
    public Guid CustomerId { get; set; }
    public int TotalPrice { get; set; } // tổng tiền trước khi giảm giá 
    
    public int TotalPriceAfterDiscount { get; set; } // tổng tiền sau khi giảm giá
    public int TotalQuantity { get; set; }
    public int TotalGram { get; set; }
    public object CartItems { get; set; } = new List<CartDetailModel>();

    public Guid VoucherId { get; set; }

    public bool IsUsingPoint { get; set; }
    
    public int VoucherDiscountPercent { get; set; }

    public int VoucherDiscount { get; set; }

    public int PointDiscount { get; set; }

    public string VoucherMessage { get; set; } = "";

    public string PointMessage { get; set; } = "";
}