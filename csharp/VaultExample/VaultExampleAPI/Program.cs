using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Vault;
using Vault.Client;
using Vault.Model;
using VaultExampleAPI;

var mountPath = Environment.GetEnvironmentVariable("SECRET_MOUNT_PATH") ?? "secret";

var builder = WebApplication.CreateSlimBuilder(args);
builder.Logging.AddConsole();

builder.Services.AddSingleton((serviceProvider) =>
{
    string address = "http://127.0.0.1:8200";
    VaultConfiguration config = new(address);

    VaultClient vaultClient = new(config);
    vaultClient.SetToken("dev-only-token");
    return vaultClient;
});

var app = builder.Build();

var sampleTodos = TodoGenerator.GenerateTodos().ToArray();

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos);
todosApi.MapGet("/{id}", (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? Results.Ok(todo)
        : Results.NotFound());

var secretsApi = app.MapGroup("/secrets");

secretsApi.MapPost("/", (VaultClient vaultClient, [FromBody] VaultData secretData) =>
{
    // Write a secret
    var kvRequestData = new KvV2WriteRequest(secretData.Data);
    vaultClient.Secrets.KvV2Write(secretData.Name, kvRequestData, mountPath);
    return Results.Ok();
});

secretsApi.MapGet("/{secretKey}", (ILoggerFactory loggerFactory, VaultClient vaultClient, string secretKey) =>
{
    var logger = loggerFactory.CreateLogger("vault");
    try
    {
        VaultResponse<KvV2ReadResponse> resp = vaultClient.Secrets.KvV2Read(secretKey, mountPath);
        var data = resp.Data.ToString();
        return !string.IsNullOrEmpty(data) ? Results.Ok(JsonSerializer.Deserialize<object>(data)) : Results.NotFound();
    }
    catch (VaultApiException ex)
    {
        logger.LogError(ex, "vault error");
        return Results.NotFound();
    }
});

app.Run();

