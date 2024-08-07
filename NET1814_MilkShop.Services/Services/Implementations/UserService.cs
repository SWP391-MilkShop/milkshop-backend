﻿using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.UserModels;
using NET1814_MilkShop.Repositories.Repositories.Interfaces;
using NET1814_MilkShop.Repositories.UnitOfWork.Interfaces;
using NET1814_MilkShop.Services.CoreHelpers;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;
using NET1814_MilkShop.Services.Services.Interfaces;

namespace NET1814_MilkShop.Services.Services.Implementations;

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

    public async Task<ResponseModel> ChangeUsernameAsync(Guid userId, ChangeUsernameModel model)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Người dùng"), null);
        }

        //check if new username is the same as the old one
        if (user.Username == model.NewUsername)
        {
            return ResponseModel.BadRequest(ResponseConstants.SameUsername);
        }

        //check if new username is already taken
        var existingUser = await _userRepository.GetByUsernameAsync(model.NewUsername, user.RoleId);
        if (existingUser != null)
        {
            return ResponseModel.BadRequest(ResponseConstants.Exist("Tên đăng nhập"));
        }

        //check if password is correct
        if (!BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            return ResponseModel.BadRequest(ResponseConstants.WrongPassword);
        }

        user.Username = model.NewUsername;
        _userRepository.Update(user);
        var result = await _unitOfWork.SaveChangesAsync();
        if (result > 0)
        {
            return ResponseModel.Success(ResponseConstants.Update("tên đăng nhập", true), null);
        }

        return ResponseModel.Error(ResponseConstants.Update("tên đăng nhập", false));
    }

    /// <summary>
    /// Admin có thể tạo tài khoản cho nhân viên hoặc admin khác
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public async Task<ResponseModel> CreateUserAsync(CreateUserModel model)
    {
        var existingUser = await _userRepository.GetByUsernameAsync(model.Username, model.RoleId);
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
            || u.FirstName != null && u.FirstName.Contains(searchTerm)
            || u.LastName != null && u.LastName.Contains(searchTerm)
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

    public async Task<ResponseModel> ChangePasswordAsync(Guid userId, ChangePasswordModel model)
    {
        if (string.Equals(model.OldPassword, model.NewPassword))
        {
            return ResponseModel.BadRequest(ResponseConstants.SamePassword);
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
        var user = await _userRepository.GetUsersQuery()
            .Include(u => u.Role).FirstOrDefaultAsync(x => x.Id == id);
        if (user == null)
        {
            return ResponseModel.BadRequest(ResponseConstants.NotFound("Người dùng"), null);
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