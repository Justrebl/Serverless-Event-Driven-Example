using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BlobTrigger
{
    public class EvgblobTrigger
    {
        private readonly ILogger<EvgblobTrigger> _logger;

        public EvgblobTrigger(ILogger<EvgblobTrigger> logger)
        {
            _logger = logger;
        }

        [Function(nameof(EvgblobTrigger))]
        public async Task Run([BlobTrigger("samples-workitems/{name}", Source = BlobTriggerSource.EventGrid, Connection = "")] Stream stream, string name)
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();
            _logger.LogInformation($"C# Blob Trigger (using Event Grid) processed blob\n Name: {name} \n Data: {content}");
        }
    }
}
