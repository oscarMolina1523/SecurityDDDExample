using AutoMapper;
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using DDDExample.Domain.Repositories;

namespace DDDExample.Application.Services;

public class ProductService : IProductService
{
    private readonly IRepository<Product, Guid> _repository;
    private readonly IMapper _mapper;

    public ProductService(IRepository<Product, Guid> repository, IMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        return product != null ? _mapper.Map<ProductDto>(product) : null;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<ProductDto>>(products);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        var product = new Product(
            id: Guid.NewGuid(),
            name: dto.Name,
            description: dto.Description,
            price: dto.Price,
            stock: dto.Stock,
            categoryId: dto.CategoryId);

        var createdProduct = await _repository.AddAsync(product, cancellationToken);
        return _mapper.Map<ProductDto>(createdProduct);
    }

    public async Task UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with ID {id} not found.");

        product.Update(dto.Name, dto.Description, dto.Price, dto.CategoryId);
        await _repository.UpdateAsync(product, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with ID {id} not found.");

        await _repository.DeleteAsync(product, cancellationToken);
    }

    public async Task UpdateStockAsync(Guid id, UpdateProductStockDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Product with ID {id} not found.");

        product.UpdateStock(dto.Quantity);
        await _repository.UpdateAsync(product, cancellationToken);
    }
}