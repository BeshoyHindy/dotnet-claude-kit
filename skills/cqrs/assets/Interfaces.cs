// CQRS Interfaces
// Copy to: src/Application/Common/CQRS/
// Requires: Result types from result-pattern skill

namespace YourNamespace.Application.Common.CQRS;

// Marker interfaces for commands and queries

/// <summary>
/// Marker for commands that return no value.
/// Commands represent intent to change state.
/// </summary>
public interface ICommand;

/// <summary>
/// Marker for commands that return a value.
/// </summary>
/// <typeparam name="TResponse">The type of value returned.</typeparam>
public interface ICommand<TResponse>;

/// <summary>
/// Marker for queries.
/// Queries retrieve data without side effects.
/// </summary>
/// <typeparam name="TResponse">The type of data returned.</typeparam>
public interface IQuery<TResponse>;

// Handler contracts

/// <summary>
/// Handles a command that returns no value.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// Handles a command that returns a value.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the response or an error.</returns>
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// Handles a query.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Handles the query.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the response or an error.</returns>
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct = default);
}

// Optional: Message dispatcher interface for decoupling

/// <summary>
/// Dispatches commands and queries to their handlers.
/// Implement with DI container or use a library like MediatR/Wolverine.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Sends a command and returns the result.
    /// </summary>
    Task<Result> SendAsync(ICommand command, CancellationToken ct = default);

    /// <summary>
    /// Sends a command and returns the result with response.
    /// </summary>
    Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a query and returns the result.
    /// </summary>
    Task<Result<TResponse>> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken ct = default);
}
