using AutoMapper;
using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using DDDExample.Domain.Repositories;

namespace DDDExample.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly IRepository<Category, string> _repository;
    private readonly IMapper _mapper;

    public CategoryService(IRepository<Category, string> repository, IMapper mapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<CategoryDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        return category != null ? _mapper.Map<CategoryDto>(category) : null;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IEnumerable<CategoryDto>>(categories);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var category = new Category(dto.Name, dto.Description);
        var createdCategory = await _repository.AddAsync(category, cancellationToken);
        return _mapper.Map<CategoryDto>(createdCategory);
    }

    public async Task UpdateAsync(string id, UpdateCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Category with ID {id} not found.");

        category.Update(dto.Name, dto.Description);
        await _repository.UpdateAsync(category, cancellationToken);
    }

    public async Task ToggleStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Category with ID {id} not found.");

        category.ToggleStatus();
        await _repository.UpdateAsync(category, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Category with ID {id} not found.");

        await _repository.DeleteAsync(category, cancellationToken);
    }
}