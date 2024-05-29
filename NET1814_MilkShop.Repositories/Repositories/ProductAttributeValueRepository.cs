using Microsoft.EntityFrameworkCore;
using NET1814_MilkShop.Repositories.Data;
using NET1814_MilkShop.Repositories.Data.Entities;

namespace NET1814_MilkShop.Repositories.Repositories;

public interface IProductAttributeValueRepository
{
    IQueryable<ProductAttributeValue> GetProductAttributeValue();
    void Add(ProductAttributeValue pav);
    void Update(ProductAttributeValue pav);
    void Remove(ProductAttributeValue pav);
    Task<Product?> GetProductById(Guid id);
    Task<ProductAttribute?> GetAttributeById(int aid);
    Task<ProductAttributeValue?> GetProdAttValue(Guid id, int aid);
}

public class ProductAttributeValueRepository : Repository<ProductAttributeValue>, IProductAttributeValueRepository
{
    public ProductAttributeValueRepository(AppDbContext context) : base(context)
    {
    }

    public IQueryable<ProductAttributeValue> GetProductAttributeValue()
    {
        return _query;
    }

    public async Task<Product?> GetProductById(Guid id)
    {
        var entity = await _context.Products.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (entity != null)
        {
            return entity;
        }

        return null;
    }

    public async Task<ProductAttribute?> GetAttributeById(int aid)
    {
        var entity = await _context.ProductAttributes.FirstOrDefaultAsync(x => x.Id == aid && x.DeletedAt == null);
        if (entity != null)
        {
            return entity;
        }

        return null;
    }

    public async Task<ProductAttributeValue?> GetProdAttValue(Guid id, int aid)
    {
        return await
            _context.ProductAttributeValues.FirstOrDefaultAsync(x =>
                x.ProductId == id && x.AttributeId == aid && x.DeletedAt == null);
    }
}