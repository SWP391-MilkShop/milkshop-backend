﻿namespace NET1814_MilkShop.Repositories.Models.ProductModels;

public class ProductStatsQueryModel
{
    /// <summary>
    /// From date (default is last 30 days)
    /// Format is mm-dd-yyyy or yyyy-mm-dd
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// To date (default is now)
    /// Format is mm-dd-yyyy or yyyy-mm-dd
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Parent id (default is root category)
    /// </summary>
    public int ParentId { get; set; } = 0;
}