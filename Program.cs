using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Graph;

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
                        currentBatchStep = 1; // batches are 1-indexed, weird
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