{
    "MSAL": {
        "TenantId": "<GUID Tenant ID>",
        "Authority": "https://login.microsoftonline.com",
        "ClientId": "GUID App Client ID",
        "ClientSecret": "***",
        "ClientObjectId": "GUID App Object ID",
        "Issuer": "<tenantName>.onmicrosoft.com",
        "userScenario": "internal" | "external"
    },
    "NewExternalUser": {
        "EmailAddress": "john.doe@domain.com",
        "DisplayName": "John Doe",
        "Password": "" #Can be set or left empty for random password generation
    },
    "NewInternalUser": {
        "DisplayName": "John Doe",
        "MailNickname": "johndoe",
        "Password": "", #Can be set or left empty for random password generation
        "UserPrincipalName": "john.doe"
    }
}