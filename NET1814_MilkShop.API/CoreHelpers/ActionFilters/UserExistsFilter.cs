﻿using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NET1814_MilkShop.Services.Services.Interfaces;

namespace NET1814_MilkShop.API.CoreHelpers.ActionFilters;

public class UserExistsFilter : IAsyncActionFilter
{
    private readonly ICustomerService _customerService;
    private readonly IUserService _userService;

    public UserExistsFilter(IServiceProvider serviceProvider)
    {
        _customerService = serviceProvider.GetRequiredService<ICustomerService>();
        _userService = serviceProvider.GetRequiredService<IUserService>();
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        // Get the user id from the claims
        var userIdStr = context
            .HttpContext.User.Claims.FirstOrDefault(c => "UserId".Equals(c.Type))
            ?.Value;
        if (userIdStr == null)
        {
            context.Result = new BadRequestObjectResult("Người dùng không tồn tại");
            return;
        }

        var userId = Guid.Parse(userIdStr);
        var roleId = context
            .HttpContext.User.Claims.FirstOrDefault(c => ClaimTypes.Role.Equals(c.Type))
            ?.Value;
        if (roleId == "1" || roleId == "2")
        {
            // Check if the admin or staff exists
            bool isExist = await _userService.IsExistAsync(userId);
            if (!isExist)
            {
                context.Result = new NotFoundObjectResult("Người dùng không tồn tại");
                return;
            }
        }
        else if (roleId == "3")
        {
            // Check if the customer exists
            bool isExist = await _customerService.IsExistAsync(userId);
            if (!isExist)
            {
                context.Result = new NotFoundObjectResult("Người dùng không tồn tại");
                return;
            }
        }

        context.HttpContext.Items.Add("UserId", userId);
        // If the user exists, continue with the next action
        await next();
    }
}