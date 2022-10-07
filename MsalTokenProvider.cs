using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace tenant_deleter
{
    // todo: stop building new MsalTokenProviders for every project
    public class MsalTokenProvider : IAuthenticationProvider
    {
        public readonly IPublicClientApplication _client;
        private readonly string[] _scopes = {
                "https://graph.microsoft.com/User.ReadWrite.All",
                "offline_access"
            };

        public MsalTokenProvider(string tenantId)
        {
            var storageProperties = new StorageCreationPropertiesBuilder("tenant_deleter", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                    .WithMacKeyChain("msal-tenant-deleter-cache", "msal-tenant-deleter-cache")
                    .Build();

            _client = PublicClientApplicationBuilder
                // mt app reg in separate tenant
                // todo: move to config
                .Create("67d892a5-2e0d-4fb5-88d4-5e5c75d774cb")
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .Build();

            var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties).Result;
            cacheHelper.RegisterCache(_client.UserTokenCache);
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            AuthenticationResult token;
            try
            {
                // get an account ------ ??????
                var account = await _client.GetAccountsAsync();
                token = await _client.AcquireTokenSilent(_scopes, account.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                token = await _client.AcquireTokenWithDeviceCode(
                    _scopes,
                    resultCallback =>
                    {
                        Console.WriteLine(resultCallback.Message);
                        return Task.CompletedTask;
                    }

                ).ExecuteAsync();
            }
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
    }
}