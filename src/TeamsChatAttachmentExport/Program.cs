using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace TeamsChatAttachmentExport
{
    internal class Program
    {
        // =========================
        // CONFIGURATION (PLACEHOLDERS)
        // =========================
        private static readonly string TenantId = "YOUR_TENANT_ID";
        private static readonly string ClientId = "YOUR_CLIENT_ID";
        private static readonly string CertificatePath = @"C:\Path\To\Certificate.pfx";
        private static readonly string CertificatePassword = "YOUR_CERT_PASSWORD";
        private static readonly string ChatId = "YOUR_CHAT_ID";

        // Safe-by-default behavior:
        // Set to true only if you explicitly want to download files
        private static readonly bool EnableDownload = false;

        private static readonly string OutputFolder = "Attachments";
        private static readonly int DelayMs = 500;

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Loading certificate...");
                var cert = new X509Certificate2(
                    CertificatePath,
                    CertificatePassword,
                    X509KeyStorageFlags.MachineKeySet
                );

                var app = ConfidentialClientApplicationBuilder
                    .Create(ClientId)
                    .WithCertificate(cert)
                    .WithTenantId(TenantId)
                    .Build();

                Console.WriteLine("Acquiring token...");
                var tokenResult = await app
                    .AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" })
                    .ExecuteAsync();

                var graphClient = new GraphServiceClient(
                    new BearerTokenAuthProvider(tokenResult.AccessToken)
                );

                Directory.CreateDirectory(OutputFolder);

                Console.WriteLine("Fetching chat messages (paginated)...");
                var allMessages = new List<ChatMessage>();

                var page = await graphClient.Chats[ChatId].Messages.GetAsync();

                while (page?.Value != null && page.Value.Count > 0)
                {
                    allMessages.AddRange(page.Value);

                    if (string.IsNullOrEmpty(page.OdataNextLink))
                        break;

                    await Task.Delay(DelayMs);

                    page = await graphClient
                        .Chats[ChatId]
                        .Messages
                        .WithUrl(page.OdataNextLink)
                        .GetAsync();
                }

                Console.WriteLine($"Total messages retrieved: {allMessages.Count}");
                Console.WriteLine("Inspecting attachments...");

                foreach (var message in allMessages)
                {
                    if (message.Attachments == null)
                        continue;

                    foreach (var attachment in message.Attachments)
                    {
                        // Debug / inspection output
                        Console.WriteLine($"Attachment: {attachment.Name ?? "(no name)"}");
                        Console.WriteLine($"  ContentType: {attachment.ContentType ?? "(null)"}");

                        var preview = attachment.Content?.ToString() ?? "(null)";
                        if (preview.Length > 120)
                            preview = preview.Substring(0, 120) + "...";

                        Console.WriteLine($"  Content preview: {preview}");
                        Console.WriteLine("----");

                        // Download only if explicitly enabled
                        if (
                            EnableDownload &&
                            attachment.ContentType == "application/vnd.microsoft.teams.file.download.info"
                        )
                        {
                            try
                            {
                                var json = JsonDocument.Parse(attachment.Content?.ToString() ?? "");
                                var downloadUrl = json.RootElement
                                    .GetProperty("downloadUrl")
                                    .GetString();

                                if (string.IsNullOrEmpty(downloadUrl))
                                    continue;

                                Console.WriteLine($"Downloading: {attachment.Name}");

                                using var http = new HttpClient();
                                var bytes = await http.GetByteArrayAsync(downloadUrl);

                                var safeName = MakeSafeFilename(attachment.Name ?? "file.bin");
                                var savePath = Path.Combine(OutputFolder, safeName);

                                await File.WriteAllBytesAsync(savePath, bytes);

                                Console.WriteLine($"Saved to: {savePath}");
                                await Task.Delay(DelayMs);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Download failed: {ex.Message}");
                            }
                        }
                    }
                }

                Console.WriteLine("Done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error:");
                Console.WriteLine(ex.Message);
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }

    // =========================
    // Minimal auth provider for Graph SDK v5
    // =========================
    public class BearerTokenAuthProvider : IAuthenticationProvider
    {
        private readonly string _token;

        public BearerTokenAuthProvider(string token)
        {
            _token = token;
        }

        public Task AuthenticateRequestAsync(
            RequestInformation request,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default
        )
        {
            request.Headers.Add("Authorization", $"Bearer {_token}");
            return Task.CompletedTask;
        }
    }
}
