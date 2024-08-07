namespace NET1814_MilkShop.Repositories.Models.UnitModels;

public class UnitModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Gram { get; set; }
    public string Description { get; set; } = null!;
    public bool IsActive { get; set; }
}