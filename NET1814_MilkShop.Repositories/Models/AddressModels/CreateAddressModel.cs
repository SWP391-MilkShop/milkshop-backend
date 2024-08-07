using System.ComponentModel.DataAnnotations;
using NET1814_MilkShop.Repositories.CoreHelpers.Regex;

namespace NET1814_MilkShop.Repositories.Models.AddressModels;

public class CreateAddressModel
{
    [Required(ErrorMessage = "Receiver name is required")]
    public string? ReceiverName { get; set; }

    [Required(ErrorMessage = "Receiver phone is required")]
    [RegularExpression(PhoneNumberRegex.Pattern, ErrorMessage = "Invalid Phone Number!")]
    public string? ReceiverPhone { get; set; }

    [Required(ErrorMessage = "ProvinceId is required")]
    public int ProvinceId { get; set; }

    [Required(ErrorMessage = "ProvinceName is required")]
    public string? ProvinceName { get; set; }

    [Required(ErrorMessage = "DistrictId is required")]
    public int DistrictId { get; set; }

    [Required(ErrorMessage = "DistrictName is required")]
    public string? DistrictName { get; set; }

    [Required(ErrorMessage = "WardId is required")]
    public string? WardCode { get; set; }

    [Required(ErrorMessage = "WardName is required")]
    public string? WardName { get; set; }

    [Required(ErrorMessage = "Address is required")]
    public string? Address { get; set; }

    public bool IsDefault { get; set; } = false;
}