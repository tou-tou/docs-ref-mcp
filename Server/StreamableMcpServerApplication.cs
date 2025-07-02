using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using DocsMcpServer.Core;

namespace DocsMcpServer.Server;

/// <summary>
/// MCP server implementation based on UnityNaturalMCP architecture
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

    public async Task RunAsync(string ipAddress = "127.0.0.1", int port = 5001, CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;

        // Add both with and without trailing slash for compatibility
        var mcpEndpoint = $"http://{ipAddress}:{port}/mcp/";
        var mcpEndpointNoSlash = $"http://{ipAddress}:{port}/mcp";
        _httpListener.Prefixes.Add(mcpEndpoint);
        try
        {
            _httpListener.Prefixes.Add(mcpEndpointNoSlash);
        }
        catch
        {
            // Ignore if already added
        }
        _httpListener.Start();
        
        _logger.LogInformation($"Started MCP server at {mcpEndpoint}");

        Pipe clientToServerPipe = new();
        Pipe serverToClientPipe = new();

        var builder = new ServiceCollection();
        
        // Copy services from the main service provider
        var documentManager = _serviceProvider.GetRequiredService<DocumentManager>();
        builder.AddSingleton(documentManager);
        
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
                                
                                _logger.LogDebug($"Received request: {inputBody}");
                                
                                if (string.IsNullOrWhiteSpace(inputBody))
                                {
                                    response.StatusCode = 400;
                                    response.Close();
                                    break;
                                }

                                var inputBodyJson = JsonNode.Parse(inputBody);
                                if (inputBodyJson?["method"]?.ToString() != "notifications/initialized")
                                {
                                    // Add clientInfo if missing (workaround for Claude's /mcp command)
                                    if (inputBodyJson?["method"]?.ToString() == "initialize" && 
                                        inputBodyJson["params"] != null && 
                                        inputBodyJson["params"]["clientInfo"] == null)
                                    {
                                        inputBodyJson["params"]["clientInfo"] = new JsonObject
                                        {
                                            ["name"] = "claude-code",
                                            ["version"] = "1.0.0"
                                        };
                                        inputBody = inputBodyJson.ToJsonString();
                                        _logger.LogDebug($"Added missing clientInfo to initialize request");
                                    }
                                    
                                    _logger.LogDebug($"Writing to pipe: {inputBody}");
                                    await clientToServerPipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(inputBody + "\n"), token);
                                    await clientToServerPipe.Writer.FlushAsync(token);

                                    _logger.LogDebug("Waiting for response from MCP server...");
                                    var result = await serverToClientPipe.Reader.ReadAsync(token);
                                    var buffer = result.Buffer;
                                    _logger.LogDebug($"Read {buffer.Length} bytes from pipe");
                                    
                                    var resultBody = Encoding.UTF8.GetString(buffer.ToArray());
                                    serverToClientPipe.Reader.AdvanceTo(buffer.End);

                                    response.ContentType = "application/json";
                                    _logger.LogDebug($"Sending response: {resultBody}");
                                    // Add newline for streaming HTTP protocol compatibility
                                    await response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(resultBody + "\n"), token);
                                }
                                response.Close();
                                break;
                            }
                        case "GET":
                            {
                                // Handle dynamic client registration for Claude Code
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