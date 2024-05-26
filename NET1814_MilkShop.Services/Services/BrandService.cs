using System.Linq.Expressions;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.BrandModels;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Repositories.UnitOfWork;
using NET1814_MilkShop.Services.CoreHelpers;

namespace NET1814_MilkShop.Services.Services;

public interface IBrandService
{
    Task<ResponseModel> GetBrandsAsync(BrandQueryModel queryModel);
    Task<ResponseModel> AddBrandAsync(BrandModel model);
    Task<ResponseModel> UpdateBrandAsync(BrandModel model);
    Task<ResponseModel> DeleteBrandAsync(int id);
}

public class BrandService : IBrandService
{
    private readonly IBrandRepository _brandRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BrandService(IBrandRepository brandRepository, IUnitOfWork unitOfWork)
    {
        _brandRepository = brandRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResponseModel> GetBrandsAsync(BrandQueryModel queryModel)
    {
        var query = _brandRepository.GetBrandsQuery();

        #region filter

        if (queryModel.IsActive == false)
        {
            query = query.Where(x => x.IsActive == false);
        }

        if (!string.IsNullOrEmpty(queryModel.SearchTerm))
        {
            query = query.Where(x =>
                x.Name.Contains(queryModel.SearchTerm) ||
                x.Description.Contains(queryModel.SearchTerm));
        }

        #endregion

        #region sort

        if ("desc".Equals(queryModel.SortOrder))
        {
            query = query.OrderByDescending(GetSortBrandProperty(queryModel));
        }
        else
        {
            query = query.OrderBy(GetSortBrandProperty(queryModel));
        }

        #endregion

        var model = query.Select(x => new BrandModel()
        {
            Id = x.Id,
            Name = x.Name,
            Description = x.Description
        });

        #region paging

        var brands = await PagedList<Brand>.CreateAsync(query, queryModel.Page, queryModel.PageSize);

        #endregion

        return new ResponseModel()
        {
            Data = brands,
            Message = brands.TotalCount > 0 ? "Get brands successfully" : "No brands found",
            Status = "Success"
        };
    }

    public async Task<ResponseModel> AddBrandAsync(BrandModel model)
    {
        // var isExistId = await _brandRepository.GetById(model.Id);
        // if (isExistId != null) //không cần check vì brandid tự tăng và không được nhập
        // {
        //     return new ResponseModel
        //     {
        //         Message = "BrandId is existed",
        //         Status = "Error"
        //     };
        // }
        var isExistName = await _brandRepository.GetBrandByName(model.Name);
        if (isExistName != null)
        {
            return new ResponseModel
            {
                Message = "Brand name is existed! Add new brand fail!",
                Status = "Error"
            };
        }

        var entity = new Brand
        {
            Name = model.Name,
            Description = model.Description,
            IsActive = true
        };
        _brandRepository.Add(entity);
        await _unitOfWork.SaveChangesAsync();
        return new ResponseModel
        {
            Status = "Success",
            Data = entity,
            Message = "Add new brand successfully"
        };
    }

    public async Task<ResponseModel> UpdateBrandAsync(BrandModel model)
    {
        var isExistId = await _brandRepository.GetById(model.Id);
        if (isExistId == null)
        {
            return new ResponseModel
            {
                Status = "Error",
                Message = "Brand not found"
            };
        }

        var isExistName = await _brandRepository.GetBrandByName(model.Name);
        if (isExistName != null)
        {
            return new ResponseModel
            {
                Status = "Error",
                Message = "Brand name is existed! Update brand fail!"
            };
        }

        isExistId.Name = model.Name;
        isExistId.Description = model.Description;
        _brandRepository.Update(isExistId);
        await _unitOfWork.SaveChangesAsync();
        return new ResponseModel
        {
            Status = "Success",
            Message = "Update brand successfully",
            Data = isExistId
        };
    }

    public async Task<ResponseModel> DeleteBrandAsync(int id)
    {
        var isExist = await _brandRepository.GetById(id);
        if (isExist == null)
        {
            return new ResponseModel
            {
                Status = "Error",
                Message = "Brand not found"
            };
        }

        isExist.DeletedAt = DateTime.Now;
        isExist.IsActive = false;
        await _unitOfWork.SaveChangesAsync();
        return new ResponseModel
        {
            Status = "Success",
            Message = "Delete brand successfully"
        };
    }


    private static Expression<Func<Brand, object>> GetSortBrandProperty(
        BrandQueryModel queryModel
    ) => queryModel.SortColumn?.ToLower() switch
    {
        "name" => product => product.Name,
        _ => product => product.Id
    };
}