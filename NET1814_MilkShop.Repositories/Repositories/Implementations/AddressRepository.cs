using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.Data;
using NET1814_MilkShop.Repositories.Data.Entities;
using NET1814_MilkShop.Repositories.Repositories.Interfaces;

namespace NET1814_MilkShop.Repositories.Repositories.Implementations;

public class AddressRepository : Repository<CustomerAddress>, IAddressRepository
{
    public AddressRepository(AppDbContext context) : base(context)
    {
    }

    public IQueryable<CustomerAddress> GetCustomerAddresses(Guid guid)
    {
        return _query
            .Where(x => x.UserId == guid);
    }

    public async Task<bool> ExistAnyAddress(Guid customerId)
    {
        return await _query.AnyAsync(x => x.UserId == customerId);
    }

    public async Task<CustomerAddress?> GetByDefault(Guid id)
    {
        return await _query
            .FirstOrDefaultAsync(x => x.UserId == id && x.IsDefault);
    }
}