using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace tenant_deleter
{
    class Program
    {
        async static Task Main(string[] args)
        {
            string tenantId;
            if (args.Length < 1)
            {
                Console.WriteLine("gimme a tenant");
                tenantId = Console.ReadLine();
            }
            else
            {
                tenantId = args[0];
            }

            var graph = new GraphServiceClient(new MsalTokenProvider(tenantId));
            var td = new ThingDeleter(graph);
            await td.DeleteAllUsersFromTenant();
            Console.WriteLine("*fin*");
            Console.ReadLine();
        }
    }

    public class MsalTokenProvider : IAuthenticationProvider
    {
        public readonly IPublicClientApplication _client;
        private readonly string[] _scopes = {
             "https://graph.microsoft.com/User.ReadWrite.All"
            };

        public MsalTokenProvider(string tenantId)
        {
            _client = PublicClientApplicationBuilder
               // mt app reg in separate tenant
               .Create("67d892a5-2e0d-4fb5-88d4-5e5c75d774cb")
               .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
               .Build();
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
            catch (MsalUiRequiredException ex)
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
            request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        }
    }

    public class ThingDeleter
    {
        private readonly GraphServiceClient _graphServiceClient;
        public ThingDeleter(GraphServiceClient client)
        {
            _graphServiceClient = client;
        }

        // delete all users from tenant
        public async Task DeleteAllUsersFromTenant()
        {
            var me = await _graphServiceClient.Me.Request().Select(x => x.Id).GetAsync();
            var users = await _graphServiceClient.Users.Request()
                .Select(x => x.Id)
                .Top(20)
            .GetAsync();

            var batch = new BatchRequestContent();
            var currentBatchStep = 1;
            var pageIterator = PageIterator<User>
            .CreatePageIterator(
                _graphServiceClient,
                users,
                (user) =>
                {
                    if (user.Id == me.Id) return true; //don't delete me
                    var requestUrl = _graphServiceClient
                            .Users[user.Id]
                            .Request().RequestUrl;

                    var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
                    var requestStep = new BatchRequestStep(currentBatchStep.ToString(), request, null);
                    batch.AddBatchRequestStep(requestStep);

                    if (currentBatchStep == users.Count)
                    {
                        _graphServiceClient.Batch.Request().PostAsync(batch).GetAwaiter().GetResult();
                        currentBatchStep = 1; // batches are 1-indexed
                        return true;
                    }
                    currentBatchStep++;
                    return true;
                },
                (req) =>
                {
                    return req;
                }
            );
            await pageIterator.IterateAsync();
        }
    }
}