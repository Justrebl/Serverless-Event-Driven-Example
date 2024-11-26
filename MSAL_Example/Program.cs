using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;

var config =
    new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

string clientId = config.GetValue("MSAL:ClientId", String.Empty);
string clientSecret = config.GetValue("MSAL:ClientSecret", String.Empty);
string authority = config.GetValue("MSAL:Authority", "https://login.microsoftonline.com/");
string clientObjectId = config.GetValue("MSAL:ClientObjectId", String.Empty);

IConfidentialClientApplication msalClient = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithAuthority(new Uri(authority))
                    .Build();

msalClient.AddInMemoryTokenCache();

AuthenticationResult msalAuthenticationResult = await msalClient.AcquireTokenForClient(new string[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();
Console.WriteLine($"Access Token: {msalAuthenticationResult.AccessToken}");
var httpClient = new HttpClient();
using var graphRequest = new HttpRequestMessage(HttpMethod.Get, $"https://graph.microsoft.com/v1.0/applications/{clientObjectId}");
graphRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", msalAuthenticationResult.AccessToken);
var graphResponseMessage = await httpClient.SendAsync(graphRequest);
graphResponseMessage.EnsureSuccessStatusCode();

using var graphResponseJson = JsonDocument.Parse(await graphResponseMessage.Content.ReadAsStreamAsync());
Console.WriteLine(JsonSerializer.Serialize(graphResponseJson, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
