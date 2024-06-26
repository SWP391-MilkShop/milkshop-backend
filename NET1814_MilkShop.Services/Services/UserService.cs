﻿using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.UserModels;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Repositories.UnitOfWork;
using NET1814_MilkShop.Services.CoreHelpers;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.CoreHelpers.Enum;

namespace NET1814_MilkShop.Services.Services
{
    public interface IUserService
    {
        Task<ResponseModel> ChangePasswordAsync(Guid userId, ChangePasswordModel model);

        /*Task<ResponseModel> GetUsersAsync();*/
        Task<ResponseModel> CreateUserAsync(CreateUserModel model);
        Task<ResponseModel> GetUsersAsync(UserQueryModel request);
        Task<ResponseModel> UpdateUserAsync(Guid id, UpdateUserModel model);
        Task<bool> IsExistAsync(Guid id);
        Task<ResponseModel> GetCustomersStatsAsync(CustomersStatsQueryModel model);
    }

    public sealed class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        /*private static UserModel ToUserModel(User user)
        {
            return new UserModel
            {
                Id = user.Id.ToString(),
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.Name,
                IsActive = user.IsActive
            };
        }*/
        /*public async Task<ResponseModel> GetUsersAsync()
        {
            var users = await _userRepository.GetUsersAsync();
            var models = users.Select(users => ToUserModel(users)).ToList();
            return new ResponseModel
            {
                Data = models,
                Message = "Get all users successfully",
                Status = "Success"
            };
        }*/

        /// <summary>
        /// Admin có thể tạo tài khoản cho nhân viên hoặc admin khác
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<ResponseModel> CreateUserAsync(CreateUserModel model)
        {
            var existingUser = await _userRepository.GetByUsernameAsync(model.Username);
            if (existingUser != null)
            {
                return ResponseModel.BadRequest(ResponseConstants.Exist("Tên đăng nhập"));
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = model.Username,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                RoleId = model.RoleId,
                IsActive = true, //no activation required
                IsBanned = false
            };
            _userRepository.Add(user);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.Create("tài khoản", true), null);
            }

            return ResponseModel.Error(ResponseConstants.Create("tài khoản", false));
        }

        public async Task<ResponseModel> GetUsersAsync(UserQueryModel request)
        {
            var query = _userRepository.GetUsersQuery();
            //filter
            var searchTerm = StringExtension.Normalize(request.SearchTerm);
            query = query.Where(u =>
                string.IsNullOrEmpty(searchTerm)
                || u.Username.ToLower().Contains(searchTerm)
                || u.FirstName.Contains(searchTerm)
                || u.LastName.Contains(searchTerm)
            );

            if (!string.IsNullOrEmpty(request.RoleIds))
            {
                var roleIds = request.RoleIds.Split(',')
                    .Select(roleIdStr => int.TryParse(roleIdStr, out var roleId) ? roleId : (int?)null)
                    .Where(roleId => roleId.HasValue)
                    .ToList();
                query = query.Where(u => roleIds.Contains(u.RoleId));
            }

            if (request.IsActive.HasValue || request.IsBanned.HasValue)
            {
                query = query.Where(u =>
                    (!request.IsActive.HasValue || u.IsActive == request.IsActive.Value)
                    && (!request.IsBanned.HasValue || u.IsBanned == request.IsBanned.Value)
                );
            }

            //sort
            query = "desc".Equals(request.SortOrder?.ToLower())
                ? query.OrderByDescending(GetSortProperty(request))
                : query.OrderBy(GetSortProperty(request));
            var result = query.Select(u => ToUserModel(u));
            //page
            var users = await PagedList<UserModel>.CreateAsync(
                result,
                request.Page,
                request.PageSize
            );
            return ResponseModel.Success(
                ResponseConstants.Get("người dùng", users.TotalCount > 0),
                users
            );
        }

        private static Expression<Func<User, object>> GetSortProperty(UserQueryModel request)
        {
            Expression<Func<User, object>> keySelector = request.SortColumn?.ToLower().Replace(" ", "") switch
            {
                "username" => user => user.Username,
                "firstname" => user => user.FirstName!,
                "lastname" => user => user.LastName!,
                "role" => user => user.Role!.Name,
                "isactive" => user => user.IsActive,
                "createdat" => user => user.CreatedAt,
                _ => user => user.Id
            };
            return keySelector;
        }

        private static UserModel ToUserModel(User user)
        {
            return new UserModel
            {
                Id = user.Id.ToString(),
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role!.Name,
                IsActive = user.IsActive,
                IsBanned = user.IsBanned
            };
        }

        public async Task<bool> IsExistAsync(Guid id)
        {
            return await _userRepository.IsExistAsync(id);
        }

        public async Task<ResponseModel> GetCustomersStatsAsync(CustomersStatsQueryModel model)
        {
            if (model.From > DateTime.Now || model.From > model.To)
            {
                return ResponseModel.BadRequest(ResponseConstants.InvalidFilterDate);
            }

            var query = _userRepository.GetUserQueryIncludeCustomer();
            // default is from last 30 days
            var from = model.From ?? DateTime.Now.AddDays(-30);
            // default is now
            var to = model.To ?? DateTime.Now;
            query = query.Where(o => o.Customer!.CreatedAt >= from && o.Customer!.CreatedAt <= to);
            // count customer who have already bought
            var totalCustomerBought = query
                .Where(x => x.Customer.Orders != null &&
                            x.Customer.Orders.Any(order => order.StatusId == (int)OrderStatusId.DELIVERED))
                .Distinct();
            var resp = new CustomerStatsModel()
            {
                TotalCustomers = await query.CountAsync(),
                TotalBoughtCustomer = await totalCustomerBought.CountAsync()
            };
            return ResponseModel.Success(ResponseConstants.Get("thống kê người dùng", true), resp);
        }

        public async Task<ResponseModel> ChangePasswordAsync(Guid userId, ChangePasswordModel model)
        {
            if (string.Equals(model.OldPassword, model.NewPassword))
            {
                return ResponseModel.BadRequest(ResponseConstants.PassSameNewPass);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Người dùng"), null);
            }

            if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, user.Password))
            {
                return ResponseModel.BadRequest(ResponseConstants.WrongPassword);
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _userRepository.Update(user);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.ChangePassword(true), null);
            }

            return ResponseModel.Error(ResponseConstants.ChangePassword(false));
        }

        public async Task<ResponseModel> UpdateUserAsync(Guid id, UpdateUserModel model)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return ResponseModel.Success(ResponseConstants.NotFound("Người dùng"), null);
            }

            user.IsBanned = model.IsBanned;
            _userRepository.Update(user);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return ResponseModel.Success(ResponseConstants.Update("người dùng", true), ToUserModel(user));
            }

            return ResponseModel.Error(ResponseConstants.Update("người dùng", false));
        }
    }
}