﻿using System.ComponentModel.DataAnnotations;

namespace NET1814_MilkShop.Repositories.Models.CategoryModels;

public class CreateCategoryModel
{
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Description is required")]
    public string Description { get; set; } = null!;

    /// <summary>
    /// Default is 0 (root category)
    /// </summary>
    public int? ParentId { get; set; } = 0;
}