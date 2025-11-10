using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace VideoFunction
{
public class BlobVideoTranscoder
{
    private readonly ILogger _logger;
    private readonly string _storageConn;

    public BlobVideoTranscoder(ILogger<BlobVideoTranscoder> logger)
    {
        _logger = logger;
        _storageConn = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
                    ?? throw new InvalidOperationException("AzureWebJobsStorage not set");
    }

    [Function("BlobVideoTranscoder")]
    public async Task RunAsync(
        [BlobTrigger("inputvideo/{name}", Connection = "AzureWebJobsStorage")] Stream inputBlob,
        string name)
    {
            var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                _logger.LogInformation($"Triggered for blob: {name} at {DateTime.UtcNow}");

                
                Directory.CreateDirectory(tmpDir);
                var inputPath = Path.Combine(tmpDir, name);
                var outputDir = Path.Combine(tmpDir, "hls_out");
                Directory.CreateDirectory(outputDir);

                // save input blob
                using (var fs = File.Create(inputPath))
                {
                    await inputBlob.CopyToAsync(fs);
                }

                // FFmpeg: transcode to HLS (m3u8 + ts)
                var ffmpegArgs = $"-y -i \"{inputPath}\" -codec: copy -start_number 0 -hls_time 10 -hls_list_size 0 -f hls \"{Path.Combine(outputDir, "index.m3u8")}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Running ffmpeg: {psi.FileName} {psi.Arguments}");
                using (var p = Process.Start(psi))
                {
                    string err = await p.StandardError.ReadToEndAsync();
                    string outp = await p.StandardOutput.ReadToEndAsync();
                    p.WaitForExit();
                    _logger.LogInformation($"ffmpeg exit code: {p.ExitCode}");
                    _logger.LogInformation($"ffmpeg stdout: {outp}");
                    _logger.LogInformation($"ffmpeg stderr: {err}");
                    if (p.ExitCode != 0)
                    {
                        throw new ApplicationException($"ffmpeg failed. Exit code {p.ExitCode}");
                    }
                }

                // upload results to output container
                var blobService = new BlobServiceClient(_storageConn);
                var outContainer = blobService.GetBlobContainerClient("outputvideo");
                await outContainer.CreateIfNotExistsAsync();

                foreach (var file in Directory.GetFiles(outputDir))
                {
                    var blobName = Path.GetFileName(file);
                    var blobClient = outContainer.GetBlobClient(Path.Combine(Path.GetFileNameWithoutExtension(name), blobName));
                    _logger.LogInformation($"Uploading {file} -> outputvideo/{blobClient.Name}");
                    using var fs = File.OpenRead(file);
                    await blobClient.UploadAsync(fs, overwrite: true);
                }

               
                _logger.LogInformation($"Processing completed for {name}");
            }
           catch(Exception ex)
            {
                _logger.LogError($"Error processing blob {name}: {ex}");
                throw;
            }
            finally
            {
                // cleanup
                try { Directory.Delete(tmpDir, true); } 
                catch(Exception ex) 
                { 
                    _logger.LogWarning($"Failed to delete temp dir {tmpDir}: {ex}");
                    throw;
                }

                // ensure cleanup in case of error
                // (could be improved with more robust temp dir management)
            }
        }
}
}