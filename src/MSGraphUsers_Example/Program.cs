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
var issuer = config.GetValue<string>("MSAL:Issuer");
string userScenario = config.GetValue("MSAL:UserScenario", "internal");

var scopes = new[] { $"https://graph.microsoft.com/.default" };
var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
var graphClient = new GraphServiceClient(credential, scopes);

// Get All the Users
try {
    var users = await graphClient.Users.GetAsync();
    var usersValue = users.Value;
    Console.WriteLine($"Users: {usersValue.Count}");
}
catch (Exception ex) {
    Console.WriteLine(ex.Message);
}

User newUser = null;
string? password = string.IsNullOrEmpty(config.GetValue<string>("NewUser:Password")) 
            ? PasswordGenerator.CreateRandomPassword(12) 
            : config.GetValue<string>("NewUser:Password");

switch(userScenario)
{
    case "internal":
        //Scenario 1 : Create a new External User
        var email = config.GetValue<string>("NewExternalUser:EmailAddress");
        var extDisplayName = config.GetValue<string>("NewExternalUser:DisplayName");
        // If password is not provided, generate a random password

        newUser = new User {
            AccountEnabled = true,
            DisplayName = extDisplayName,
            Mail = email,
            Identities = new List<ObjectIdentity>
            {
                new ObjectIdentity
                {
                    SignInType = "emailAddress",
                    Issuer = issuer,
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
        break;

    case "external":
        // Scenario 2 : Create a new Internal User
        var internalUser = config.GetValue<string>("NewInternalUser:UserPrincipalName");
        var intMailNickname = config.GetValue<string>("NewInternalUser:MailNickname");
        var intDisplayName = config.GetValue<string>("NewInternalUser:DisplayName");
        var intUserPrincipalName = config.GetValue<string>("NewInternalUser:UserPrincipalName");

        //Check mandatory configs are provided
        if (string.IsNullOrEmpty(internalUser) || string.IsNullOrEmpty(intMailNickname) || string.IsNullOrEmpty(intDisplayName) || string.IsNullOrEmpty(intUserPrincipalName)) {
            Console.WriteLine("Please provide all the mandatory fields for internal user creation");
            return;
        }

        newUser = new User {
            AccountEnabled = true,
            DisplayName = intDisplayName,
            MailNickname = intMailNickname,
            UserPrincipalName=$"{internalUser}@{issuer}", // needs to be the same domain as the tenant as it will be a member of the tenant, not an external user in the tenant
            PasswordProfile = new PasswordProfile
            {
                Password = password,
                ForceChangePasswordNextSignIn = true
            }
        };
        break;
    default: 
        Console.WriteLine("Invalid User Scenario");
        break;
}

try {
    if (newUser != null) {
        var result = await graphClient.Users.PostAsync(newUser);
        Console.WriteLine($"User Successfully created with ID: {result.Id}");
    }
    else {
        Console.WriteLine("No relevant information provided for the user, exiting...");
    }
}
catch (Exception ex) {
    Console.WriteLine(ex.Message);
}