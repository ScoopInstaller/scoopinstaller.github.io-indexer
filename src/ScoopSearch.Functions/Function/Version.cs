using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace ScoopSearch.Functions.Function;

public class Version
{
    [FunctionName("Version")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
        HttpRequest request,
        ILogger log)
    {
        var assembly = GetType().Assembly;
        var assemblyLocation = assembly.Location;

        return new OkObjectResult($"{assembly.GetName().Name} - {FileVersionInfo.GetVersionInfo(assemblyLocation).ProductVersion} - {File.GetLastWriteTimeUtc(assemblyLocation):R}");
    }
}
