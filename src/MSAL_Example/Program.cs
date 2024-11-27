using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Text.Json;
using Microsoft.Graph.Models;
using Azure.Identity;
using Microsoft.Graph;
using MSAL_Example.utils;

var config =
    new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

string tenantId = config.GetValue("MSAL:TenantId", String.Empty);
string clientId = config.GetValue("MSAL:ClientId", String.Empty);
string clientSecret = config.GetValue("MSAL:ClientSecret", String.Empty);
string authorityBase = config.GetValue("MSAL:Authority", "https://login.microsoftonline.com/");
Uri authority = new Uri(new Uri(authorityBase), tenantId); // Double new Uri to avoid adding a second trailing slash in case authority already has one.

var scopes = new[] { $"https://graph.microsoft.com/.default" };
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graphClient = new GraphServiceClient(credential, scopes);

// Scenario 1 : Get All the Users
try {
    var users = await graphClient.Users.GetAsync();
    var usersValue = users.Value;
    Console.WriteLine($"Users: {usersValue.Count}");
}
catch (Exception ex) {
    Console.WriteLine(ex.Message);
}

//Scenario 2 : Create a new User
var email = config.GetValue("NewUser:EmailAddress", "JohnDoe@outlook.fr");
var displayName = config.GetValue("NewUser:DisplayName", "John Doe");
// If password is not provided, generate a random password
string? password = string.IsNullOrEmpty(config.GetValue<string>("NewUser:Password")) 
    ? PasswordGenerator.CreateRandomPassword(12) 
    : config.GetValue<string>("NewUser:Password");

var newUser = new User {
    AccountEnabled = true,
    DisplayName = "Test User",
    Mail = email,
    Identities = new List<ObjectIdentity>
    {
        new ObjectIdentity
        {
            SignInType = "emailAddress",
            Issuer = "justrebldemo.onmicrosoft.com",
            IssuerAssignedId = email
        }
    },
    PasswordProfile = new PasswordProfile
    {
        Password = password,
        ForceChangePasswordNextSignIn = false // Need to be false for localUsers
    },
    PasswordPolicies = "DisablePasswordExpiration" //Need to be disabled for locaUsers
};
try {
    var result = await graphClient.Users.PostAsync(newUser);
    Console.WriteLine($"User Successfully created with ID: {result.Id}");
}
catch (Exception ex) {
    Console.WriteLine(ex.Message);
}