using ECafe.Application.Repository;
using ECafe.Domain.Entities.Base;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ECafe.Infrastructure.Repositories
{
    public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public BaseRepository(ECafeDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();

        }
        public async Task<TEntity> Add(TEntity model)
        {
            await _dbSet.AddAsync(model);
            return model;
        }

        public Task AddRangeAsync(ICollection<TEntity> entities)
        {
            _dbSet.AddRange(entities);
            return Task.CompletedTask;
        }

        public Task Update(TEntity model)
        {
            _dbSet.Update(model);
            return Task.CompletedTask;
        }

        public Task Delete(TEntity model)
        {
            if (model is ISoftDelete softDeleteEntity)
            {
                softDeleteEntity.IsDeleted = true;
                _dbSet.Update(model);
            }
            else
            {
                _dbSet.Remove(model);
            }

            return Task.CompletedTask;
        }

        public Task<TEntity?> GetByIdAsync<T>(T id)
            => _dbSet.FindAsync(id).AsTask();

        public IQueryable<TEntity> GetAll()
            => _dbSet.AsNoTracking();

        public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate)
            => _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);

        public Task<List<TEntity>> GetWhereAsync(Expression<Func<TEntity, bool>> predicate)
            => _dbSet.AsNoTracking().Where(predicate).ToListAsync();

        public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>>? predicate = null)
        {
            var query = _dbSet.AsNoTracking().AsQueryable();
            return predicate is null ? query : query.Where(predicate);
        }

        public Task<int> SaveChangesAsync()
            => _context.SaveChangesAsync();

        public IQueryable<TEntity> QueryTracked(Expression<Func<TEntity, bool>>? predicate = null)
        {
            var query = _dbSet.AsQueryable(); // tracking
            return predicate is null ? query : query.Where(predicate);
        }

        public Task<bool> CheckExistAsync(Expression<Func<TEntity, bool>>? predicate = null)
        {
            if (predicate == null)
                return _dbSet.AnyAsync();

            return _dbSet.AnyAsync(predicate);
        }
    }
}
