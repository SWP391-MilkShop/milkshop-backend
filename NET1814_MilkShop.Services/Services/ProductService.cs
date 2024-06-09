﻿using System.Linq.Expressions;
using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.CoreHelpers.Enum;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.ProductModels;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Repositories.UnitOfWork;
using NET1814_MilkShop.Services.CoreHelpers;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;

namespace NET1814_MilkShop.Services.Services
{
    public interface IProductService
    {
        Task<ResponseModel> GetProductsAsync(ProductQueryModel queryModel);
        Task<ResponseModel> GetProductByIdAsync(Guid id);
        Task<ResponseModel> CreateProductAsync(CreateProductModel model);
        Task<ResponseModel> UpdateProductAsync(Guid id, UpdateProductModel model);
        Task<ResponseModel> DeleteProductAsync(Guid id);
    }

    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IUnitRepository _unitRepository;
        private readonly IProductStatusRepository _productStatusRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ProductService(
            IProductRepository productRepository,
            IBrandRepository brandRepository,
            ICategoryRepository categoryRepository,
            IUnitRepository unitRepository,
            IProductStatusRepository productStatusRepository,
            IUnitOfWork unitOfWork
        )
        {
            _productRepository = productRepository;
            _brandRepository = brandRepository;
            _categoryRepository = categoryRepository;
            _unitRepository = unitRepository;
            _productStatusRepository = productStatusRepository;
            _unitOfWork = unitOfWork;
        }

        private static ProductModel ToProductModel(Product product) =>
            new ProductModel
            {
                Id = product.Id.ToString(),
                Name = product.Name,
                Description = product.Description,
                Quantity = product.Quantity,
                Brand = product.Brand!.Name,
                Category = product.Category!.Name,
                Unit = product.Unit!.Name,
                OriginalPrice = product.OriginalPrice,
                SalePrice = product.SalePrice,
                Status = product.ProductStatus!.Name,
                Thumbnail = product.Thumbnail,
                IsActive = product.IsActive
            };

        public async Task<ResponseModel> GetProductsAsync(ProductQueryModel queryModel)
        {
            var query = _productRepository.GetProductsQuery();
            //Normalize search term, brand, category, unit, status
            var searchTerm = StringExtension.Normalize(queryModel.SearchTerm);
            var brand = StringExtension.Normalize(queryModel.Brand);
            var category = StringExtension.Normalize(queryModel.Category);
            var unit = StringExtension.Normalize(queryModel.Unit);
            var status = StringExtension.Normalize(queryModel.Status);
            #region Filter, Search

            //thu gọn thành 1 where thôi
            query = query.Where(p =>
                (queryModel.IsActive.HasValue ? p.IsActive == queryModel.IsActive.Value : true)
                //search theo name, description, brand, unit, category
                && (
                    string.IsNullOrEmpty(searchTerm)
                    || p.Name.ToLower().Contains(searchTerm)
                    || p.Description!.Contains(searchTerm)
                    || p.Brand!.Name.Contains(searchTerm)
                    || p.Unit!.Name.Contains(searchTerm)
                    || p.Category!.Name.Contains(searchTerm)
                )
                //filter theo brand, category, unit, status, minPrice, maxPrice
                && (string.IsNullOrEmpty(brand) || string.Equals(p.Brand!.Name, brand))
                && (string.IsNullOrEmpty(category) || string.Equals(p.Category!.Name, category))
                && (string.IsNullOrEmpty(unit) || string.Equals(p.Unit!.Name, unit))
                && (string.IsNullOrEmpty(status) || string.Equals(p.ProductStatus!.Name, status))
                && (queryModel.MinPrice <= 0 || p.SalePrice >= queryModel.MinPrice)
                && (queryModel.MaxPrice <= 0 || p.SalePrice <= queryModel.MaxPrice)
            );

            #endregion

            #region Sort

            if ("desc".Equals(queryModel.SortOrder?.ToLower()))
            {
                query = query.OrderByDescending(GetSortProperty(queryModel));
            }
            else
            {
                query = query.OrderBy(GetSortProperty(queryModel));
            }

            #endregion

            #region Convert to ProductModel

            var productModelQuery = query.Select(p => ToProductModel(p));

            #endregion

            #region Pagination

            var products = await PagedList<ProductModel>.CreateAsync(
                productModelQuery,
                queryModel.Page,
                queryModel.PageSize
            );

            #endregion


            return ResponseModel.Success(
                ResponseConstants.Get("sản phẩm", products.TotalCount > 0),
                products
            );
        }

        /// <summary>
        /// Get sort property as expression
        /// </summary>
        /// <param name="queryModel"></param>
        /// <returns></returns>
        private static Expression<Func<Product, object>> GetSortProperty(
            ProductQueryModel queryModel
        ) =>
            queryModel.SortColumn?.ToLower().Replace(" ", "") switch
            {
                "name" => product => product.Name,
                "saleprice" => product => product.SalePrice,
                "quantity" => product => product.Quantity,
                "createdat" => product => product.CreatedAt,
                _ => product => product.Id,
            };

        public async Task<ResponseModel> GetProductByIdAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Sản phẩm"), null);
            }
            return ResponseModel.Success(
                ResponseConstants.Get("sản phẩm", true),
                ToProductModel(product)
            );
        }

        public async Task<ResponseModel> CreateProductAsync(CreateProductModel model)
        {
            if(model.SalePrice > model.OriginalPrice)
            {
                return ResponseModel.BadRequest(ResponseConstants.InvalidSalePrice);
            }
            #region Validate Brand, Category, Unit exist
            var brand = await _brandRepository.GetByIdAsync(model.BrandId);
            if (brand == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Thương hiệu"), null);
            }
            var category = await _categoryRepository.GetByIdAsync(model.CategoryId);
            if (category == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Danh mục"), null);
            }
            var unit = await _unitRepository.GetByIdAsync(model.UnitId);
            if (unit == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Đơn vị"), null);
            }
            #endregion
            var product = new Product
            {
                Name = model.Name,
                Description = model.Description,
                Quantity = model.Quantity,
                OriginalPrice = model.OriginalPrice,
                SalePrice = model.SalePrice,
                BrandId = model.BrandId,
                CategoryId = model.CategoryId,
                UnitId = model.UnitId,
                StatusId = (int) ProductStatusId.SELLING, //default status is selling
                IsActive = true, // default is active (published)
                Thumbnail = model.Thumbnail
            };
            _productRepository.Add(product);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.Create("sản phẩm", true), null);
            }
            return ResponseModel.Error(ResponseConstants.Create("sản phẩm", false));
        }

        public async Task<ResponseModel> UpdateProductAsync(Guid id, UpdateProductModel model)
        {
            if (model.SalePrice > model.OriginalPrice)
            {
                return ResponseModel.BadRequest(ResponseConstants.InvalidSalePrice);
            }
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Sản phẩm"), null);
            }
            if (!string.IsNullOrEmpty(model.Name))
            {
                var productByName = await _productRepository.GetByNameAsync(model.Name);
                if (productByName != null && productByName.Id != id)
                {
                    return ResponseModel.Success(ResponseConstants.Exist("Tên sản phẩm"), null);
                }
                product.Name = model.Name;
            }
            #region Validate Brand, Category, Unit, Status exist
            if (model.BrandId.HasValue)
            {
                var brand = await _brandRepository.GetByIdAsync(model.BrandId.Value);
                if (brand == null)
                {
                    return ResponseModel.Success(ResponseConstants.NotFound("Thương hiệu"), null);
                }
                product.BrandId = model.BrandId.Value;
            }
            if (model.CategoryId.HasValue)
            {
                var category = await _categoryRepository.GetByIdAsync(model.CategoryId.Value);
                if (category == null)
                {
                    return ResponseModel.Success(ResponseConstants.NotFound("Danh mục"), null);
                }
                product.CategoryId = model.CategoryId.Value;
            }
            if (model.UnitId.HasValue)
            {
                var unit = await _unitRepository.GetByIdAsync(model.UnitId.Value);
                if (unit == null)
                {
                    return ResponseModel.Success(ResponseConstants.NotFound("Đơn vị"), null);
                }
                product.UnitId = model.UnitId.Value;
            }
            if (model.StatusId.HasValue)
            {
                var status = await _productStatusRepository.GetByIdAsync(model.StatusId.Value);
                if (status == null)
                {
                    return ResponseModel.Success(ResponseConstants.NotFound("Trạng thái"), null);
                }
                product.StatusId = model.StatusId.Value;
            }
            #endregion
            product.Description = string.IsNullOrEmpty(model.Description)
                ? product.Description
                : model.Description;
            product.Quantity = model.Quantity ?? product.Quantity;
            product.OriginalPrice = model.OriginalPrice ?? product.OriginalPrice;
            product.SalePrice = model.SalePrice ?? product.SalePrice;
            if (!string.IsNullOrEmpty(model.Thumbnail))
            {
                if (!Uri.IsWellFormedUriString(model.Thumbnail, UriKind.Absolute))
                {
                    return ResponseModel.BadRequest(ResponseConstants.WrongFormat("URL"));
                }
                product.Thumbnail = model.Thumbnail;
            }
            product.IsActive = model.IsActive;
            _productRepository.Update(product);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.Update("sản phẩm", true), null);
            }
            return ResponseModel.Error(ResponseConstants.Update("sản phẩm", false));
        }

        public async Task<ResponseModel> DeleteProductAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Sản phẩm"), null);
            }
            _productRepository.Delete(product);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.Delete("sản phẩm", true), null);
            }
            return ResponseModel.Error(ResponseConstants.Delete("sản phẩm", false));
        }
    }
}
