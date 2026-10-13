using System.Linq.Expressions;

namespace OlympiaStudent360.Api.Core.Services;

public static class QueryableFilterExtensions
{
    /// <summary>
    /// Builds explicit equality branches instead of Enumerable.Contains. The MySQL EF
    /// provider used by this application cannot assign a relational type mapping to a
    /// captured collection in an IN expression.
    /// </summary>
    public static IQueryable<TEntity> WhereMatchesAny<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, string?>> selector,
        IEnumerable<string> values,
        Expression<Func<TEntity, bool>>? fallback = null)
    {
        Expression body = Expression.Constant(false);
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            body = Expression.OrElse(body, Expression.Equal(
                selector.Body, Expression.Constant(value, typeof(string))));
        }

        if (fallback != null)
        {
            var fallbackBody = new ReplaceParameterVisitor(
                fallback.Parameters[0], selector.Parameters[0]).Visit(fallback.Body)!;
            body = Expression.OrElse(body, fallbackBody);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    /// <summary>Numeric equivalent for bounded report selections.</summary>
    public static IQueryable<TEntity> WhereMatchesAnyValue<TEntity, TValue>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TValue>> selector,
        IEnumerable<TValue> values) where TValue : struct
    {
        Expression body = Expression.Constant(false);
        foreach (var value in values.Distinct())
        {
            body = Expression.OrElse(body, Expression.Equal(
                selector.Body, Expression.Constant(value, typeof(TValue))));
        }
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters));
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}
