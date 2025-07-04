using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using DocsRef.Core;

namespace DocsRef.Server;

/// <summary>
/// HTTP-based MCP server implementation for streaming communication
/// </summary>
public sealed class StreamableMcpServerApplication : IDisposable
{
    private readonly HttpListener _httpListener = new();
    private readonly ILogger<StreamableMcpServerApplication> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cancellationTokenSource;

    public StreamableMcpServerApplication(IServiceProvider serviceProvider, ILogger<StreamableMcpServerApplication> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _httpListener.Close();
        _cancellationTokenSource?.Dispose();
    }

    public async Task RunAsync(string ipAddress = "127.0.0.1", int port = 7334, CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;

        // Configure HTTP endpoint
        var mcpEndpoint = $"http://{ipAddress}:{port}/mcp/";
        _httpListener.Prefixes.Add(mcpEndpoint);
        _httpListener.Start();
        
        _logger.LogInformation($"Started MCP server at {mcpEndpoint}");

        Pipe clientToServerPipe = new();
        Pipe serverToClientPipe = new();

        var builder = new ServiceCollection();
        
        // Copy required services from the main service provider
        var documentManager = _serviceProvider.GetRequiredService<DocumentManager>();
        builder.AddSingleton(documentManager);
        
        // Copy web-related services
        var webPageDownloader = _serviceProvider.GetRequiredService<WebPageDownloader>();
        builder.AddSingleton(webPageDownloader);
        
        var htmlToMarkdownConverter = _serviceProvider.GetRequiredService<HtmlToMarkdownConverter>();
        builder.AddSingleton(htmlToMarkdownConverter);
        
        // Copy logging
        builder.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            var logLevelStr = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT") ?? "Information";
            if (Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
            {
                loggingBuilder.SetMinimumLevel(logLevel);
            }
        });
        
        // Add MCP server
        builder.AddMcpServer()
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream())
            .WithToolsFromAssembly();

        _logger.LogInformation("Loaded MCP tools from assembly");

        await using var services = builder.BuildServiceProvider();

        // Start handling HTTP requests
        _ = Task.Run(() => HandleHttpRequestAsync(_httpListener, clientToServerPipe, serverToClientPipe, token), token);

        var mcpServer = services.GetRequiredService<IMcpServer>();
        await mcpServer.RunAsync(token);
    }

    private async Task HandleHttpRequestAsync(HttpListener listener, Pipe clientToServerPipe, 
        Pipe serverToClientPipe, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                try
                {
                    switch (request.HttpMethod)
                    {
                        case "POST":
                            {
                                using var inputReader = new StreamReader(request.InputStream, Encoding.UTF8);
                                var inputBody = await inputReader.ReadLineAsync();
                                
                                if (string.IsNullOrWhiteSpace(inputBody))
                                {
                                    response.StatusCode = 400;
                                    response.Close();
                                    break;
                                }

                                var inputBodyJson = JsonNode.Parse(inputBody);
                                if (inputBodyJson?["method"]?.ToString() != "notifications/initialized")
                                {
                                    // Ensure clientInfo is present for initialize requests
                                    if (inputBodyJson?["method"]?.ToString() == "initialize" && inputBodyJson["params"] is JsonObject paramsObj)
                                    {
                                        if (paramsObj["clientInfo"] == null)
                                        {
                                            paramsObj["clientInfo"] = new JsonObject
                                            {
                                                ["name"] = "claude-code",
                                                ["version"] = "1.0.0"
                                            };
                                            inputBody = inputBodyJson.ToJsonString();
                                        }
                                    }
                                    
                                    // Write request to MCP server
                                    await clientToServerPipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(inputBody + "\n"), token);
                                    await clientToServerPipe.Writer.FlushAsync(token);

                                    // Read response from MCP server
                                    var result = await serverToClientPipe.Reader.ReadAsync(token);
                                    var buffer = result.Buffer;
                                    
                                    var resultBody = Encoding.UTF8.GetString(buffer.ToArray());
                                    serverToClientPipe.Reader.AdvanceTo(buffer.End);

                                    // Send response with newline for streaming HTTP protocol
                                    response.ContentType = "application/json";
                                    await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(resultBody + "\n"), token);
                                }
                                response.Close();
                                break;
                            }
                        case "GET":
                            {
                                // Return server information for dynamic client registration
                                var serverInfo = new
                                {
                                    name = "docs-ref",
                                    version = "1.0.0",
                                    protocolVersion = "0.1.0",
                                    capabilities = new
                                    {
                                        tools = new { },
                                        logging = new { }
                                    }
                                };

                                response.ContentType = "application/json";
                                var responseBody = System.Text.Json.JsonSerializer.Serialize(serverInfo, new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                                    WriteIndented = true
                                });

                                // Add newline for streaming HTTP protocol compatibility
                                var responseBytes = Encoding.UTF8.GetBytes(responseBody + "\n");
                                await response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length, token);
                                response.Close();
                                break;
                            }
                        default:
                            response.StatusCode = 405;
                            response.Close();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request: {Message}", ex.Message);
                    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                    response.StatusCode = 500;
                    
                    // Send error response
                    var errorResponse = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        error = new
                        {
                            code = -32603,
                            message = "Internal error: " + ex.Message,
                            data = new { stackTrace = ex.StackTrace }
                        },
                        id = (object?)null
                    });
                    
                    response.ContentType = "application/json";
                    // Add newline for streaming HTTP protocol compatibility
                    var errorBytes = Encoding.UTF8.GetBytes(errorResponse + "\n");
                    await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length, token);
                    response.Close();
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Ignore for cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HTTP request handler");
        }
    }
}