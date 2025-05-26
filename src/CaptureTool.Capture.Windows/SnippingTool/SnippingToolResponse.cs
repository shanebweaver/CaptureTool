using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace CaptureTool.Capture.Windows.SnippingTool;

// https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-snipping-tool
public sealed partial class SnippingToolResponse
{
    // The HTTP status code equivalent to provide a more granular understanding of the outcome.
    // 200 Success - The operation was successful.
    // 400 Bad Request - Invalid or Missing Parameters - The request cannot be processed due to client error.
    // 408 Request Timeout - Operation Took Too Long - The operation timed out before completion.
    // 499 Client Closed Request - User Cancelled the Snip - The user cancelled the snip, closing the request.
    // 500 Internal Server Error - Processing Failed - An error occurred on the server, preventing completion.
    public required int Code { get; set; }

    // The outcome and explanation for the snip.
    public required string Reason { get; set; }

    // A unique identifier value attached to requests and messages that
    // allows reference to a particular transaction or event chain.
    public required string CorrelationId { get; set; }

    // A token representing the captured media, which the application can use to access the file.
    // Use the SharedStorageAccessManager library to redeem the token.
    // A sharing token can only be redeemed once. After that, the token is no longer valid.
    public string? FileAccessToken { get; set; }

    private StorageFile? _fileCopy;
    private readonly SemaphoreSlim _semaphore = new(1,1);

    public async Task<StorageFile> GetFileAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_fileCopy == null)
            {
                if (string.IsNullOrEmpty(FileAccessToken))
                {
                    throw new InvalidOperationException("Failed to retrieve file. FileAccessToken is null");
                }

                StorageFile file = await SharedStorageAccessManager.RedeemTokenForFileAsync(FileAccessToken);

                int attempt = 0;
                while (_fileCopy == null)
                {
                    try
                    {
                        _fileCopy = await file.CopyAsync(ApplicationData.Current.TemporaryFolder);
                    }
                    catch (Exception)
                    {
                        attempt++;
                        if (attempt == 3)
                        {
                            throw;
                        }
                    }
                }
            }

            return _fileCopy;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public static SnippingToolResponse CreateFromUri(Uri responseUri)
    {
        var queryParams = ParseQuery(responseUri.Query);
        return new SnippingToolResponse
        {
            Code = int.Parse(queryParams["code"]),
            Reason = queryParams["reason"],
            CorrelationId = queryParams["x-request-correlation-id"],
            FileAccessToken = queryParams.TryGetValue("file-access-token", out string? fileAccessToken) ? fileAccessToken : null,
        };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>();

        if (query.StartsWith('?'))
        {
            query = query.Substring(1); // Remove leading '?'
        }

        string[] pairs = query.Split('&');
        foreach (var pair in pairs)
        {
            string[] keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                result[keyValue[0]] = keyValue[1];
            }
        }

        return result;
    }
}