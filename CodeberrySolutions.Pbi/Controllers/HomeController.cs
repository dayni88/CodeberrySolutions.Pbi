using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CodeberrySolutions.Pbi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace CodeberrySolutions.Pbi.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly PowerBiSettings _powerBiSettings;

        public HomeController(ILogger<HomeController> logger, IOptions<PowerBiSettings> powerBiOptions)
        {
            _logger = logger;
            _powerBiSettings = powerBiOptions.Value;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        public async Task<IActionResult> Report()
        {
            try
            {
                var powerBISettings = _powerBiSettings;
                var result = new PowerBiEmbedConfig { Username = powerBISettings.UserName };
                var accessToken = await GetPowerBiAccessToken();
                var tokenCredentials = new TokenCredentials(accessToken, "Bearer");

                using (var client = new PowerBIClient(new Uri(powerBISettings.ApiUrl), tokenCredentials))
                {
                    var workspaceId = powerBISettings.WorkspaceId;
                    var reportId = powerBISettings.ReportId;
                    var report = await client.Reports.GetReportInGroupAsync(workspaceId.Value, reportId);
                    var generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
                    var tokenResponse = await client.Reports.GenerateTokenAsync(workspaceId.Value, reportId, generateTokenRequestParameters,CancellationToken.None);

                    result.EmbedToken = tokenResponse;
                    result.EmbedUrl = report.EmbedUrl;
                    result.Id = report.Id.ToString();
                }

                return View(result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [HttpGet]
        [AllowAnonymous]
        [ResponseCache(Duration = 20)]
        public async Task<IActionResult> GetPbiToken()
        {
            var result = new PowerBiEmbedConfig { Username = _powerBiSettings.UserName };
            var accessToken = await GetPowerBiAccessToken();
            var tokenCredentials = new TokenCredentials(accessToken, "Bearer");
            var workspaceId = _powerBiSettings.WorkspaceId;
            var reportId = _powerBiSettings.ReportId;

            using (var client = new PowerBIClient(new Uri(_powerBiSettings.ApiUrl), tokenCredentials))
            {

                var report = await client.Reports.GetReportInGroupAsync(workspaceId.Value, reportId);
                var generateTokenRequestParameters = new GenerateTokenRequest(accessLevel: "view");
                var tokenResponse = await client.Reports.GenerateTokenAsync(workspaceId.Value, reportId, generateTokenRequestParameters);

                result.EmbedToken = tokenResponse;
                result.EmbedUrl = report.EmbedUrl;
                result.Id = report.Id.ToString();
            }
            return Ok(new
            {
                result.EmbedToken.Token,
                result.EmbedToken.TokenId,
                result.EmbedToken.Expiration,
                result.EmbedUrl,
                WorkspaceId = workspaceId,
                ReportId = reportId
            });
        }

        private async Task<string> GetPowerBiAccessToken()
        {
            using (var client = new HttpClient())
            {
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["resource"] = _powerBiSettings.ResourceUrl,
                    ["username"] = _powerBiSettings.UserName,
                    ["password"] = _powerBiSettings.Password,
                    ["client_id"] = _powerBiSettings.ApplicationId.ToString(),
                    ["client_secret"] = _powerBiSettings.ApplicationSecret,
                    ["scope"] = "openid"
                };

                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

                using (var formContent = new FormUrlEncodedContent(form))
                {
                    using (var response = await client.PostAsync(_powerBiSettings.AuthorityUrl, formContent))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var jsonBody = JObject.Parse(body);

                        var errorToken = jsonBody.SelectToken("error");
                        if (errorToken != null)
                        {
                            throw new Exception(errorToken.Value<string>());
                        }

                        return jsonBody.SelectToken("access_token").Value<string>();
                    }
                }
            }
        }


    }
}
