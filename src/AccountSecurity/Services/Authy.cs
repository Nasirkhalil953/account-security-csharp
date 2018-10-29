using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AccountSecurity.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AccountSecurity.Services {
    public interface IAuthy
    {
        Task<string> registerUserAsync(RegisterViewModel user);
        Task<TokenVerificationResult> verifyTokenAsync(string authyId, string token);
        Task<TokenVerificationResult> verifyPhoneTokenAsync(string phoneNumber, string countryCode, string token);
        Task<string> sendSmsAsync(string authyId);
        Task<string> phoneVerificationCallRequestAsync(ApplicationUser currentUser);
    }

    public class Authy: IAuthy {
        private readonly IConfiguration Configuration;
        private readonly IHttpClientFactory ClientFactory;
        private readonly ILogger<Authy> logger;
        private readonly HttpClient client;

        public string message { get; private set; }

        public Authy(IConfiguration config, IHttpClientFactory clientFactory, ILoggerFactory loggerFactory) {
            Configuration = config;
            logger = loggerFactory.CreateLogger<Authy>();

            ClientFactory = clientFactory;
            client = ClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.authy.com");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("user-agent", "Twilio Account Security C# Sample");

            // Get Authy API Key from Configuration
            client.DefaultRequestHeaders.Add("X-Authy-API-Key", Configuration["AuthyApiKey"]);
        }

        public async Task<string> registerUserAsync(RegisterViewModel user) {
            var userRegData = new Dictionary<string, string>() {
                { "email", user.Email },
                { "country_code", user.CountryCode },
                { "cellphone", user.PhoneNumber }
            };
            var userRegRequestData = new Dictionary<string, object>() {};
            userRegRequestData.Add("user", userRegData);
            var encodedContent = new FormUrlEncodedContent(userRegData);

            var result = await client.PostAsJsonAsync("/protected/json/users/new", userRegRequestData);

            logger.LogDebug(result.Content.ReadAsStringAsync().Result);

            result.EnsureSuccessStatusCode();

            var response = await result.Content.ReadAsAsync<Dictionary<string, object>>();

            return JObject.FromObject(response["user"])["id"].ToString();
        }

        public async Task<TokenVerificationResult> verifyTokenAsync(string authyId, string token)
        {
            var result = await client.GetAsync($"/protected/json/verify/{token}/{authyId}");

            logger.LogDebug(result.ToString());
            logger.LogDebug(result.Content.ReadAsStringAsync().Result);
           
            result.EnsureSuccessStatusCode();

            var message = await result.Content.ReadAsStringAsync();

            return new TokenVerificationResult(message);
        }

        public async Task<TokenVerificationResult> verifyPhoneTokenAsync(string phoneNumber, string countryCode, string token)
        {
            var result = await client.GetAsync(
                $"/protected/json/phones/verification/check?phone_number={phoneNumber}&country_code={countryCode}&verification_code={token}"
            );

            logger.LogDebug(result.ToString());
            logger.LogDebug(result.Content.ReadAsStringAsync().Result);

            var message = await result.Content.ReadAsStringAsync();

            if(result.StatusCode == HttpStatusCode.OK) {
                return new TokenVerificationResult(message);
            }

            return new TokenVerificationResult(message, false);
        }

        public async Task<string> sendSmsAsync(string authyId) {
            var result = await client.GetAsync($"/protected/json/sms/{authyId}?force=true");

            logger.LogDebug(result.ToString());

            result.EnsureSuccessStatusCode();

            return await result.Content.ReadAsStringAsync();
        }

        public async Task<string> phoneVerificationCallRequestAsync(ApplicationUser user)
        {
            var result = await client.PostAsync(
                $"/protected/json/phones/verification/start?via=call&country_code={user.CountryCode}&phone_number={user.PhoneNumber}",
                null
            );

            var content = await result.Content.ReadAsStringAsync();

            logger.LogDebug(result.ToString());
            logger.LogDebug(content);

            result.EnsureSuccessStatusCode();

            return await result.Content.ReadAsStringAsync();
        }
    }

    public class TokenVerificationResult
    {
        public TokenVerificationResult(string message, bool succeeded = true)
        {
            this.Message = message;
            this.Succeeded = succeeded;
        }

        public bool Succeeded { get; set; }
        public string Message { get; set; }
    }
}