﻿namespace NET1814_MilkShop.Repositories.Models.UserModels;

public class ChangeUserInfoModel
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    //[DataType(DataType.PhoneNumber, ErrorMessage = "Invalid Phone Number!")]
    //[RegularExpression(@"^([0-9]{10})$", ErrorMessage = "Invalid Phone Number!")]
    public string? PhoneNumber { get; set; }

    //[Url(ErrorMessage = "Invalid URL!")]
    public string? ProfilePictureUrl { get; set; }
}