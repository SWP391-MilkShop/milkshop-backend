﻿using System.ComponentModel.DataAnnotations;

namespace NET1814_MilkShop.Repositories.Models.OrderModels
{
    public class OrderQueryModel : QueryModel
    {
        [Range(
            typeof(decimal),
            "0",
            "79228162514264337593543950335",
            ErrorMessage = "Total amount must be >= 0"
        )]
        public decimal TotalAmount { get; set; } = 0;

        public string? Email { get; set; }

        /// <summary>
        /// Format is mm-dd-yyyy or yyyy-mm-dd
        /// </summary>
        public DateTime? FromOrderDate { get; set; }

        /// <summary>
        /// Format is mm-dd-yyyy or yyyy-mm-dd
        /// </summary>
        public DateTime? ToOrderDate { get; set; }

        public string? PaymentMethod { get; set; }
        public string? OrderStatus { get; set; }

        /// <summary>
        /// Sort by id, total amount, order date, payment date (default is id)
        /// </summary>
        public new string? SortColumn { get; set; }
    }
}