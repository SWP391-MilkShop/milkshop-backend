﻿using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.CategoryModels;
using NET1814_MilkShop.Repositories.Repositories.Interfaces;
using NET1814_MilkShop.Repositories.UnitOfWork.Interfaces;
using NET1814_MilkShop.Services.CoreHelpers;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;
using NET1814_MilkShop.Services.Services.Interfaces;

namespace NET1814_MilkShop.Services.Services.Implementations;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork,
        IProductRepository productRepository)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _productRepository = productRepository;
    }

    public async Task<ResponseModel> CreateCategoryAsync(CreateCategoryModel model)
    {
        if (model.ParentId != null && model.ParentId != 0)
        {
            var parentCategory = await _categoryRepository.GetByIdAsync(model.ParentId.Value);
            if (parentCategory == null)
            {
                return ResponseModel.BadRequest(ResponseConstants.NotFound("Danh mục cha"));
            }
        }

        var isExist = await _categoryRepository.IsExistAsync(model.Name, model.ParentId);
        if (isExist)
        {
            return ResponseModel.BadRequest("Danh mục đã tồn tại dưới danh mục cha này");
        }

        var category = new Category
        {
            Name = model.Name,
            Description = model.Description,
            ParentId = model.ParentId == 0 ? null : model.ParentId,
            IsActive = true
        };
        _categoryRepository.Add(category);
        var result = await _unitOfWork.SaveChangesAsync();
        if (result > 0)
        {
            return ResponseModel.Success(ResponseConstants.Create("danh mục", true), null);
        }

        return ResponseModel.Error(ResponseConstants.Create("danh mục", false));
    }

    public async Task<ResponseModel> DeleteCategoryAsync(int id)
    {
        var categories = await _categoryRepository.GetCategoriesQuery().ToListAsync();
        var category = categories.FirstOrDefault(x => x.Id == id);
        if (category == null)
        {
            return ResponseModel.Success(ResponseConstants.NotFound("Danh mục"), null);
        }

        // Check if category is in used by any product
        var isInUsed = await _productRepository.IsExistIdByCategory(id);
        if (isInUsed)
        {
            return ResponseModel.BadRequest(ResponseConstants.InUsed("danh mục"));
        }

        var children = _categoryRepository.GetChildCategoryIds(id, categories);
        if (children.Count > 1) // the children included the category itself
        {
            return ResponseModel.BadRequest("Danh mục này có danh mục con");
        }

        category.DeletedAt = DateTime.Now;
        _categoryRepository.Update(category);
        var result = await _unitOfWork.SaveChangesAsync();
        if (result > 0)
        {
            return ResponseModel.Success(ResponseConstants.Delete("danh mục", true), null);
        }

        return ResponseModel.Error(ResponseConstants.Delete("danh mục", false));
    }

    public async Task<ResponseModel> GetCategoriesAsync(CategoryQueryModel queryModel)
    {
        var query = _categoryRepository.GetCategoriesQuery()
            .Include(x => x.Parent).AsQueryable();
        var searchTerm = StringExtension.Normalize(queryModel.SearchTerm);
        query = query.Where(p =>
            (!queryModel.IsActive.HasValue || p.IsActive == queryModel.IsActive)
            && (queryModel.ParentId == 0 || p.ParentId == queryModel.ParentId)
            && (string.IsNullOrEmpty(searchTerm) || p.Name.ToLower().Contains(searchTerm))
        );
        if ("desc".Equals(queryModel.SortOrder?.ToLower()))
        {
            query = query.OrderByDescending(GetSortProperty(queryModel));
        }
        else
        {
            query = query.OrderBy(GetSortProperty(queryModel));
        }

        var categoryModelQuery = query.Select(c => new CategoryModel
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            ParentId = c.ParentId,
            ParentName = c.Parent != null ? c.Parent.Name : null,
            IsActive = c.IsActive
        });
        var categories = await PagedList<CategoryModel>.CreateAsync(
            categoryModelQuery,
            queryModel.Page,
            queryModel.PageSize
        );
        return ResponseModel.Success(
            ResponseConstants.Get("danh mục", categories.TotalCount > 0),
            categories
        );
    }

    private static Expression<Func<Category, object>> GetSortProperty(
        CategoryQueryModel queryModel
    ) =>
        queryModel.SortColumn?.ToLower().Replace(" ", "") switch
        {
            "name" => category => category.Name,
            _ => category => category.Id,
        };

    public async Task<ResponseModel> GetCategoryByIdAsync(int id)
    {
        var query = _categoryRepository.GetCategoriesQuery()
            .Include(x => x.Parent).AsQueryable();
        var category = await query.FirstOrDefaultAsync(x => x.Id == id);
        if (category is null)
        {
            return ResponseModel.Success(ResponseConstants.NotFound("Danh mục"), null);
        }

        var categoryModel = new CategoryModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            ParentName = category.Parent?.Name,
            IsActive = category.IsActive
        };
        return ResponseModel.Success(ResponseConstants.Get("danh mục", true), categoryModel);
    }

    public async Task<ResponseModel> UpdateCategoryAsync(int id, UpdateCategoryModel model)
    {
        var existingCategory = await _categoryRepository.GetByIdAsync(id);
        if (existingCategory == null)
        {
            return ResponseModel.Success(ResponseConstants.NotFound("Danh mục"), null);
        }

        if (model.ParentId != 0)
        {
            var categories = await _categoryRepository.GetCategoriesQuery().ToListAsync();
            if (categories.All(c => c.Id != model.ParentId))
            {
                return ResponseModel.BadRequest("Danh mục cha không tồn tại");
            }

            if (_categoryRepository.IsAncestorOf(model.ParentId, id, categories))
            {
                return ResponseModel.BadRequest("Danh mục cha này là danh mục con của danh mục hiện tại");
            }

            existingCategory.ParentId = model.ParentId;
        }

        if (!string.IsNullOrEmpty(model.Name))
        {
            // Check if category name is changed
            if (!string.Equals(model.Name, existingCategory.Name, StringComparison.OrdinalIgnoreCase))
            {
                var isExist = await _categoryRepository.IsExistAsync(model.Name, existingCategory.ParentId);
                if (isExist)
                {
                    return ResponseModel.BadRequest("Danh mục đã tồn tại dưới danh mục cha này");
                }
            }

            existingCategory.Name = model.Name;
        }

        existingCategory.Description = string.IsNullOrEmpty(model.Description)
            ? existingCategory.Description
            : model.Description;
        existingCategory.IsActive = model.IsActive;
        _categoryRepository.Update(existingCategory);
        var result = await _unitOfWork.SaveChangesAsync();
        return result > 0
            ? ResponseModel.Success(ResponseConstants.Update("danh mục", true), null)
            : ResponseModel.Error(ResponseConstants.Update("danh mục", false));
    }
}