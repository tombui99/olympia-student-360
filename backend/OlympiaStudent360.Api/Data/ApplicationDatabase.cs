using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;

namespace OlympiaStudent360.Api.Data;

public static class ApplicationDatabase
{
    public static DbContextOptionsBuilder<TContext> UseResilientMySql<TContext>(
        this DbContextOptionsBuilder<TContext> options, string connectionString) where TContext : DbContext
    {
        UseResilientMySql((DbContextOptionsBuilder)options, connectionString);
        return options;
    }

    public static DbContextOptionsBuilder UseResilientMySql(
        this DbContextOptionsBuilder options, string connectionString) =>
        options.UseMySQL(connectionString, mysql => mysql.ExecutionStrategy(
            dependencies => new SessionAwareExecutionStrategy(dependencies)));

    public static IExecutionStrategy CreateRetryStrategy(DbContext db) =>
        new TransientExecutionStrategy(db);

    private sealed class TransientExecutionStrategy : ExecutionStrategy
    {
        public TransientExecutionStrategy(DbContext db) : base(db, 3, TimeSpan.FromSeconds(5)) { }
        public TransientExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, 3, TimeSpan.FromSeconds(5)) { }

        protected override bool ShouldRetryOn(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is OperationCanceledException) return false;
                if (current is TimeoutException) return true;
                if (current is System.IO.IOException or System.Net.Sockets.SocketException) return true;
                // Connection loss, server overload/restart, lock wait timeout, and
                // deadlock. Constraint, authentication, and SQL errors are permanent.
                if (current is MySqlException mysql && (mysql.IsTransient ||
                    mysql.Number is 1040 or 1042 or 1053 or 1205 or 1213 or 2002 or 2003 or 2006 or 2013 or 2055))
                    return true;
            }
            return false;
        }
    }

    public static async Task ReleaseSessionLockAsync(System.Data.Common.DbConnection connection, string name)
    {
        if (connection.State != ConnectionState.Open) return;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RELEASE_LOCK(@name)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = name;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Discard a session whose lock could not be released. Cleanup must not
            // hide the transient failure that triggered a retry of the whole unit.
            Console.Error.WriteLine($"Could not release database lock {name}: {exception.Message}");
            if (connection is MySqlConnection mysql) MySqlConnection.ClearPool(mysql);
            await connection.DisposeAsync();
        }
    }

    // A reconnect loses MySQL named locks, and retrying one statement cannot replay a
    // transaction. Explicitly opened sessions are retried by their owner as a whole,
    // with a fresh context and reacquired locks. Ordinary request queries and saves
    // use the provider's bounded transient-failure retries.
    private sealed class SessionAwareExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : IExecutionStrategy
    {
        private readonly DbContext db = dependencies.CurrentContext.Context;
        private readonly IExecutionStrategy retry = new TransientExecutionStrategy(dependencies);
        private readonly IExecutionStrategy once = new NonRetryingExecutionStrategy(dependencies);

        private IExecutionStrategy Strategy
        {
            get
            {
                // DatabaseFacade dependencies include this execution strategy. Access
                // the scoped connection directly to avoid recursive service resolution
                // when EF asks RetriesOnFailure during context initialization.
                var connection = db.GetService<IRelationalConnection>();
                return connection.CurrentTransaction is not null ||
                    connection.DbConnection.State != ConnectionState.Closed ? once : retry;
            }
        }

        public bool RetriesOnFailure => Strategy.RetriesOnFailure;

        public TResult Execute<TState, TResult>(TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded) =>
            Strategy.Execute(state, operation, verifySucceeded);

        public Task<TResult> ExecuteAsync<TState, TResult>(TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default) =>
            Strategy.ExecuteAsync(state, operation, verifySucceeded, cancellationToken);
    }
}
