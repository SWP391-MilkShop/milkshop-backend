﻿using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.UserModels;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Repositories.UnitOfWork;
using NET1814_MilkShop.Services.CoreHelpers;

namespace NET1814_MilkShop.Services.Services
{
    public interface IUserService
    {
        Task<ResponseModel> ChangePasswordAsync(Guid userId, ChangePasswordModel model);
        /*Task<ResponseModel> GetUsersAsync();*/
        Task<ResponseModel> GetUsersAsync(UserQueryModel request);
        Task<bool> IsExistAsync(Guid id);
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

        public async Task<ResponseModel> GetUsersAsync(UserQueryModel request)
        {
            var query = _userRepository.GetUsersQuery();
            //filter
            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                query = query.Where(u => u.Username.Contains(request.SearchTerm)
                                         || u.FirstName!.Contains(request.SearchTerm)
                                         || u.LastName!.Contains(request.SearchTerm));
            }
            if (!string.IsNullOrEmpty(request.Role))
            {
                query = query.Where(u => string.Equals(u.Role!.Name, request.Role));
            }
            if (request.IsActive != null)
            {
                query = query.Where(u => u.IsActive == request.IsActive);
            }
            //sort
            query = "desc".Equals(request.SortOrder?.ToLower()) ? query.OrderByDescending(GetSortProperty(request))
                     : query.OrderBy(GetSortProperty(request));
            var result = query.Select(u => new UserModel
            {
                Id = u.Id.ToString(),
                Username = u.Username,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role!.Name,
                IsActive = u.IsActive
            });
            //page
            var users = await PagedList<UserModel>.CreateAsync(result, request.Page, request.PageSize);
            return new ResponseModel()
            {
                Data = users,
                Message = users.TotalCount > 0 ? "Get users successfully" : "No users found",
                Status = "Success"
            };
        }

        private static Expression<Func<User, object>> GetSortProperty(UserQueryModel request)
        {
            Expression<Func<User, object>> keySelector = request.SortColumn?.ToLower() switch
            {
                "username" => user => user.Username,
                "firstName" => user => user.FirstName!,
                "lastName" => user => user.LastName!,
                "role" => user => user.Role!.Name,
                "isActive" => user => user.IsActive,
                _ => user => user.Id
            };
            return keySelector;
        }

        public async Task<bool> IsExistAsync(Guid id)
        {
            return await _userRepository.IsExistAsync(id);
        }

        public async Task<ResponseModel> ChangePasswordAsync(Guid userId, ChangePasswordModel model)
        {
            if (string.Equals(model.OldPassword, model.NewPassword))
            {
                return new ResponseModel
                {
                    Message = "Old password and new password are the same",
                    Status = "Error"
                };
            }
            var user = await _userRepository.GetById(userId);
            if (user == null)
            {
                return new ResponseModel
                {
                    Message = "User not found",
                    Status = "Error"
                };
            }
            if (!BCrypt.Net.BCrypt.Verify(model.OldPassword, user.Password))
            {
                return new ResponseModel
                {
                    Message = "Old password is incorrect",
                    Status = "Error"
                };
            }
            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _userRepository.Update(user);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                return new ResponseModel
                {
                    Message = "Change password successfully",
                    Status = "Success"
                };
            }
            return new ResponseModel
            {
                Message = "Change password failed",
                Status = "Error"
            };
        }
    }
}
