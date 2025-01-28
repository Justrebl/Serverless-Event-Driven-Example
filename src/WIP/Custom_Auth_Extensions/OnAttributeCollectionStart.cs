using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Custom_Auth_Extensions
{
    public class OnAttributeCollectionStart
    {
        private readonly ILogger<OnAttributeCollectionStart> _logger;

        public OnAttributeCollectionStart(ILogger<OnAttributeCollectionStart> logger)
        {
            _logger = logger;
        }

        [Function("OnAttributeCollectionStart")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
