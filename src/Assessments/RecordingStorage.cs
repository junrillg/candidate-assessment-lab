using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CandidateAssessment.Assessments;

internal sealed record VerifiedUpload(string TemporaryPath, long Size, string Sha256, string ContentType);
internal sealed class InvalidRecordingException(string message) : Exception(message);
internal sealed class RecordingStorage
{
    private readonly string root;
    internal const long MaximumBytes = 10 * 1024 * 1024;
    public RecordingStorage(IConfiguration configuration)
    {
        root = Path.GetFullPath(configuration["Storage:Root"] ?? ".local/recordings");
        CreatePrivateDirectory(root);
        CreatePrivateDirectory(Path.Combine(root, "pending"));
    }
    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    internal async Task<VerifiedUpload> VerifyAsync(Stream body, string contentType, CancellationToken cancellationToken)
    {
        contentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (contentType is not ("audio/webm" or "audio/ogg" or "audio/mp4")) throw new InvalidRecordingException("Use WebM, Ogg, or MP4 audio.");
        var temporaryPath = Path.Combine(root, "pending", Guid.NewGuid().ToString("N"));
        try
        {
            long size = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, BufferSize = 65536, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var target = new FileStream(temporaryPath, fileOptions))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MaximumBytes) throw new InvalidRecordingException("Recording exceeds the 10 MB limit.");
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await target.FlushAsync(cancellationToken);
                target.Flush(true);
            }
            if (size == 0) throw new InvalidRecordingException("The recording is empty.");
            using var probe = JsonDocument.Parse(await RunAsync("ffprobe", ["-v", "error", "-show_format", "-show_streams", "-of", "json", temporaryPath], cancellationToken));
            var streams = probe.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            if (streams.Length != 1 || streams[0].GetProperty("codec_type").GetString() != "audio") throw new InvalidRecordingException("The file must contain exactly one audio track and no video.");
            var format = probe.RootElement.GetProperty("format").GetProperty("format_name").GetString() ?? "";
            var matching = contentType switch { "audio/webm" => format.Contains("webm"), "audio/ogg" => format == "ogg", _ => format.Contains("mp4") };
            if (!matching) throw new InvalidRecordingException("The stored audio format does not match its content type.");
            // Browser WebM can omit duration; decoding verifies both usable bytes and the actual audio length.
            var progress = await RunAsync("ffmpeg", ["-nostdin", "-v", "error", "-i", temporaryPath, "-map", "0:a:0", "-t", "121", "-f", "null", "-progress", "pipe:1", "-"], cancellationToken);
            var duration = progress.Split('\n').Where(x => x.StartsWith("out_time_us=", StringComparison.Ordinal))
                .Select(x => long.TryParse(x[12..].Trim(), CultureInfo.InvariantCulture, out var value) ? value : 0).DefaultIfEmpty().Max() / 1_000_000d;
            if (duration <= 0 || duration > 120) throw new InvalidRecordingException("Record between a moment and two minutes of audio.");
            return new VerifiedUpload(temporaryPath, size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), contentType);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }
    internal string Commit(VerifiedUpload upload, Guid recordingId)
    {
        var destination = Path.Combine(root, recordingId.ToString("N"));
        File.Move(upload.TemporaryPath, destination, false);
        return destination;
    }
    internal RecordingContent? Open(Recording recording)
    {
        if (!File.Exists(recording.Path)) return null;
        return new RecordingContent(new FileStream(recording.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true), recording.ContentType, recording.Size);
    }
    internal void Delete(string path) => File.Delete(path);
    internal async Task RemoveOrphansAsync(HashSet<string> retainedPaths, CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!retainedPaths.Contains(path) && File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddHours(-1)) File.Delete(path);
        }
        await Task.CompletedTask;
    }
    private static async Task<string> RunAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Audio validation could not start.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var text = await output;
            await error;
            if (process.ExitCode != 0) throw new InvalidRecordingException("The audio file could not be decoded.");
            return text;
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
}
