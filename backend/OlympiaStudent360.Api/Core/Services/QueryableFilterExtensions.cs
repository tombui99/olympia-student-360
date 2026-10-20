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
        var branches = new List<Expression>();
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            branches.Add(Expression.Equal(
                selector.Body, Expression.Constant(value, typeof(string))));
        }

        if (fallback != null)
        {
            var fallbackBody = new ReplaceParameterVisitor(
                fallback.Parameters[0], selector.Parameters[0]).Visit(fallback.Body)!;
            branches.Add(fallbackBody);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(BalancedOr(branches), selector.Parameters));
    }

    /// <summary>Numeric equivalent for bounded report selections.</summary>
    public static IQueryable<TEntity> WhereMatchesAnyValue<TEntity, TValue>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TValue>> selector,
        IEnumerable<TValue> values) where TValue : struct
    {
        var branches = new List<Expression>();
        foreach (var value in values.Distinct())
        {
            branches.Add(Expression.Equal(
                selector.Body, Expression.Constant(value, typeof(TValue))));
        }
        return query.Where(Expression.Lambda<Func<TEntity, bool>>(BalancedOr(branches), selector.Parameters));
    }

    // A left-deep OR chain over a large Canvas selection can overflow EF's recursive SQL
    // translator. Keep the same predicate with logarithmic expression depth.
    private static Expression BalancedOr(List<Expression> branches)
    {
        if (branches.Count == 0) return Expression.Constant(false);
        while (branches.Count > 1)
        {
            var next = new List<Expression>((branches.Count + 1) / 2);
            for (var index = 0; index < branches.Count; index += 2)
                next.Add(index + 1 < branches.Count
                    ? Expression.OrElse(branches[index], branches[index + 1])
                    : branches[index]);
            branches = next;
        }
        return branches[0];
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == source ? target : base.VisitParameter(node);
    }
}
