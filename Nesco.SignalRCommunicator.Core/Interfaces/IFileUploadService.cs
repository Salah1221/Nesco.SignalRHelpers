namespace Nesco.SignalRCommunicator.Core.Interfaces;

/// <summary>
/// Defines a contract for uploading large response data to the server.
/// Implementations can use HTTP, cloud storage, or any other mechanism for file uploads.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Uploads file data as a byte array to the specified folder.
    /// </summary>
    /// <param name="fileData">The file content as a byte array.</param>
    /// <param name="fileName">The name of the file to create.</param>
    /// <param name="folder">The folder where the file should be uploaded. Defaults to "signalr-temp".</param>
    /// <returns>The relative or absolute path to the uploaded file.</returns>
    /// <exception cref="Exception">Thrown when the upload fails.</exception>
    Task<string> UploadFileAsync(byte[] fileData, string fileName, string folder = "signalr-temp");

    /// <summary>
    /// Uploads file data from a stream to the specified folder.
    /// </summary>
    /// <param name="stream">The stream containing the file data.</param>
    /// <param name="fileName">The name of the file to create.</param>
    /// <param name="folder">The folder where the file should be uploaded. Defaults to "signalr-temp".</param>
    /// <returns>The relative or absolute path to the uploaded file.</returns>
    /// <exception cref="Exception">Thrown when the upload fails.</exception>
    Task<string> UploadStreamAsync(Stream stream, string fileName, string folder = "signalr-temp");

    /// <summary>
    /// Deletes a previously uploaded file.
    /// </summary>
    /// <param name="filePath">The path to the file to delete.</param>
    /// <returns>True if the file was successfully deleted, false otherwise.</returns>
    Task<bool> DeleteFileAsync(string filePath);
}
