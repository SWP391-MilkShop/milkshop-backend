namespace NET1814_MilkShop.Repositories.Models.OrderModels;

public class CheckoutOrderDetailModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal ItemPrice { get; set; }
    public string? ThumbNail { get; set; }
}