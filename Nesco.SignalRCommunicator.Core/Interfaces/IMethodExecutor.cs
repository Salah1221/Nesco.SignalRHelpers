namespace Nesco.SignalRCommunicator.Core.Interfaces;

/// <summary>
/// Defines a contract for executing methods on the client side based on method names.
/// Implementations should route method calls to appropriate handlers and return results.
/// </summary>
public interface IMethodExecutor
{
    /// <summary>
    /// Executes a method identified by its name with the provided parameter.
    /// </summary>
    /// <param name="methodName">The name of the method to execute.</param>
    /// <param name="parameter">The parameter to pass to the method. Can be null.</param>
    /// <returns>The result of the method execution, or null if the method returns no data.</returns>
    /// <exception cref="NotSupportedException">Thrown when the method name is not supported.</exception>
    /// <exception cref="ArgumentException">Thrown when the parameter cannot be converted to the expected type.</exception>
    Task<object?> ExecuteAsync(string methodName, object? parameter);
}
