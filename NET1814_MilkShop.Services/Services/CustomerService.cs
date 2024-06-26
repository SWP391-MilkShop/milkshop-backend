﻿using NET1814_MilkShop.Repositories.CoreHelpers.Constants;
using NET1814_MilkShop.Repositories.CoreHelpers.Enum;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Models;
using NET1814_MilkShop.Repositories.Models.UserModels;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Repositories.UnitOfWork;
using NET1814_MilkShop.Services.CoreHelpers;
using NET1814_MilkShop.Services.CoreHelpers.Extensions;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace NET1814_MilkShop.Services.Services
{
    public interface ICustomerService
    {
        /*Task<ResponseModel> GetCustomersAsync();*/
        Task<ResponseModel> GetCustomersAsync(CustomerQueryModel request);
        Task<ResponseModel> GetByEmailAsync(string email);
        Task<ResponseModel> GetByIdAsync(Guid id);
        Task<ResponseModel> ChangeInfoAsync(Guid userId, ChangeUserInfoModel changeUserInfoModel);
        Task<bool> IsExistAsync(Guid id);

        /*Task<bool> IsCustomerExistAsync(string email, string phoneNumber);*/
        Task<bool> IsExistPhoneNumberAsync(string phoneNumber);
        Task<bool> IsExistEmailAsync(string email);
    }

    public sealed class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
        {
            _customerRepository = customerRepository;
            _unitOfWork = unitOfWork;
        }

        private static CustomerModel ToCustomerModel(Customer customer, User user)
        {
            return new CustomerModel
            {
                UserId = customer.UserId.ToString(),
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                GoogleId = customer.GoogleId,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email,
                Points = customer.Points,
                Role = user.Role!.Name,
                ProfilePictureUrl = customer.ProfilePictureUrl,
                IsActive = user.IsActive,
                IsBanned = user.IsBanned
            };
        }

        public async Task<ResponseModel> GetCustomersAsync(CustomerQueryModel request)
        {
            var query = _customerRepository.GetCustomersQuery().Where(c => c.User.RoleId == (int) RoleId.CUSTOMER);
            var searchTerm = StringExtension.Normalize(request.SearchTerm);
            //filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.User.Username.ToLower().Contains(searchTerm)
                    || c.Email!.Contains(searchTerm)
                    || c.PhoneNumber!.Contains(searchTerm)
                    || c.User.FirstName.Contains(searchTerm)
                    || c.User.LastName.Contains(searchTerm)
                );
            }

            if (request.IsActive.HasValue || request.IsBanned.HasValue)
            {
                query = query.Where(c =>
                    (!request.IsActive.HasValue || c.User.IsActive == request.IsActive.Value)
                    && (!request.IsBanned.HasValue || c.User.IsBanned == request.IsBanned.Value)
                );
            }
            //sort
            query = "desc".Equals(request.SortOrder?.ToLower())
                ? query.OrderByDescending(GetSortProperty(request))
                : query.OrderBy(GetSortProperty(request));
            var result = query.Select(c => ToCustomerModel(c, c.User));
            var customers = await PagedList<CustomerModel>.CreateAsync(
                result,
                request.Page,
                request.PageSize
            );
            return ResponseModel.Success(
                ResponseConstants.Get("khách hàng", customers.TotalCount > 0),
                customers
            );
        }

        private static Expression<Func<Customer, object>> GetSortProperty(
            CustomerQueryModel request
        )
        {
            Expression<Func<Customer, object>> keySelector = request.SortColumn?.ToLower().Replace(" ", "") switch
            {
                "point" => customer => customer.Points,
                "email" => customer => customer.Email!,
                "isactive" => customer => customer.User.IsActive,
                "firstname" => customer => customer.User.FirstName!,
                "lastname" => customer => customer.User.LastName!,
                "createdat" => customer => customer.User.CreatedAt,
                _ => customer => customer.UserId
            };
            return keySelector;
        }

        public async Task<ResponseModel> GetByEmailAsync(string email)
        {
            var customer = await _customerRepository.GetByEmailAsync(email);
            if (customer == null)
            {
                /*return new ResponseModel
                {
                    Data = null,
                    Message = "Customer not found",
                    Status = "Error"
                };*/
                return ResponseModel.Success(
                    ResponseConstants.Get("khách hàng bằng email", false),
                    null
                );
            }
            var customerModel = ToCustomerModel(customer, customer.User);
            /*return new ResponseModel
            {
                Data = customerModel,
                Message = "Get customer by email successfully",
                Status = "Success"
            };*/
            return ResponseModel.Success(
                ResponseConstants.Get("khách hàng bằng email", true),
                customerModel
            );
        }

        public async Task<ResponseModel> GetByIdAsync(Guid id)
        {
            var customer = await _customerRepository.GetByIdAsync(id);
            if (customer == null)
            {
                /*return new ResponseModel { Message = "Customer not found", Status = "Error" };*/
                return ResponseModel.Success(ResponseConstants.Get("khách hàng", false), null);
            }
            var customerModel = ToCustomerModel(customer, customer.User); /*
            return new ResponseModel
            {
                Data = customerModel,
                Message = "Get customer by id successfully",
                Status = "Success"
            };*/
            return ResponseModel.Success(ResponseConstants.Get("khách hàng", true), customerModel);
        }

        public async Task<ResponseModel> ChangeInfoAsync(
            Guid userId,
            ChangeUserInfoModel changeUserInfoModel
        )
        {
            var customer = await _customerRepository.GetByIdAsync(userId);
            if (customer == null)
            {
                /*return new ResponseModel { Message = "Customer not found", Status = "Error" };*/
                return ResponseModel.Success(ResponseConstants.Get("khách hàng", false), null);
            }

            if (!string.IsNullOrWhiteSpace(changeUserInfoModel.PhoneNumber))
            {
                if (!Regex.IsMatch(changeUserInfoModel.PhoneNumber, @"^([0-9]{10})$"))
                {
                    /*return new ResponseModel
                    {
                        Message = "Invalid Phone Number!",
                        Status = "Error"
                    };*/
                    return ResponseModel.BadRequest(ResponseConstants.InvalidPhoneNumber);
                }
                customer.PhoneNumber = changeUserInfoModel.PhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(changeUserInfoModel.ProfilePictureUrl))
            {
                if (
                    !Uri.IsWellFormedUriString(
                        changeUserInfoModel.ProfilePictureUrl,
                        UriKind.Absolute
                    )
                )
                {
                    /*return new ResponseModel { Message = "Invalid URL!", Status = "Error" };*/
                    return ResponseModel.BadRequest(ResponseConstants.InvalidUrl);
                }
                customer.ProfilePictureUrl = changeUserInfoModel.ProfilePictureUrl;
            }

            if (!string.IsNullOrWhiteSpace(changeUserInfoModel.FirstName))
            {
                customer.User.FirstName = changeUserInfoModel.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(changeUserInfoModel.LastName))
            {
                customer.User.LastName = changeUserInfoModel.LastName;
            }

            _customerRepository.Update(customer);
            var result = await _unitOfWork.SaveChangesAsync();
            if (result > 0)
            {
                /*return new ResponseModel
                {
                    Data = ToCustomerModel(customer, customer.User),
                    Message = "Change user info successfully",
                    Status = "Success"
                };*/
                return ResponseModel.Success(
                    ResponseConstants.ChangeInfo(true),
                    ToCustomerModel(customer, customer.User)
                );
            }
            /*return new ResponseModel { Message = "Change user info failed", Status = "Error" };*/
            return ResponseModel.Error(ResponseConstants.ChangeInfo(false));
        }

        public async Task<bool> IsExistAsync(Guid id)
        {
            return await _customerRepository.IsExistAsync(id);
        }

        public async Task<bool> IsExistPhoneNumberAsync(string phoneNumber)
        {
            return await _customerRepository.IsExistPhoneNumberAsync(phoneNumber);
        }

        public async Task<bool> IsExistEmailAsync(string email)
        {
            return await _customerRepository.IsExistEmailAsync(email);
        }

        /*public async Task<bool> IsCustomerExistAsync(string email, string phoneNumber)
        {
            return await _customerRepository.IsCustomerExistAsync(email, phoneNumber);
        }*/
    }
}
