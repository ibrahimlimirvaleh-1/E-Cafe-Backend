using System.Linq.Expressions;

namespace ECafe.Application.Repository
{
    public interface IBaseRepository<TEntity>
    {
        Task<TEntity> Add(TEntity model);
        Task AddRangeAsync(ICollection<TEntity> entities);
        Task Update(TEntity model);
        Task Delete(TEntity model);

        Task<TEntity?> GetByIdAsync<T>(T id);
        IQueryable<TEntity> GetAll();
        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
        Task<List<TEntity>> GetWhereAsync(Expression<Func<TEntity, bool>> predicate);
        IQueryable<TEntity> Query(Expression<Func<TEntity, bool>>? predicate = null);

        IQueryable<TEntity> QueryTracked(Expression<Func<TEntity, bool>>? predicate = null);

        Task<int> SaveChangesAsync();
    }
}
