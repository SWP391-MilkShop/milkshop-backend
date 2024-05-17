﻿using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.Data;
using NET1814_MilkShop.Repositories.Data.Entities;

namespace NET1814_MilkShop.Repositories.Repositories
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByEmailAsync(string email);
        void Add(Customer customer);
        void Update(Customer customer);
        void Remove(Customer customer);
    }
    public sealed class CustomerRepository : Repository<Customer>, ICustomerRepository
    {
        public CustomerRepository(AppDbContext context)
            : base(context)
        {
        }
        public async Task<Customer?> GetByEmailAsync(string email)
        {
            return await _context.Customers.FirstOrDefaultAsync(x => x.Email == email);
        }
    }
}