namespace Nesco.SignalRCommunicator.Core.Interfaces;

/// <summary>
/// Defines a contract for reading uploaded files on the server side.
/// Implementations should handle reading from local storage, cloud storage, or other file sources.
/// </summary>
public interface IFileReaderService
{
    /// <summary>
    /// Reads the content of a file at the specified path.
    /// </summary>
    /// <param name="filePath">The relative or absolute path to the file.</param>
    /// <returns>The file content as a string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="Exception">Thrown when reading the file fails.</exception>
    Task<string> ReadFileAsync(string filePath);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="filePath">The relative or absolute path to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Deletes a file at the specified path if it exists.
    /// </summary>
    /// <param name="filePath">The relative or absolute path to the file.</param>
    /// <returns>True if the file was successfully deleted, false otherwise.</returns>
    Task<bool> DeleteFileAsync(string filePath);
}
