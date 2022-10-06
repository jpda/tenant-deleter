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
            // logging, ugh - generic host?
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
            var td = new ThingDeleter(new ThingDeleterConfig() { GraphClient = graph, MaxPageSize = 1000 });

            await td.DeleteAllUsersFromTenant();
            await td.DeleteAllApplicationsFromTenant();
            await td.DeleteAllServicePrincipalsFromTenant();

            Console.WriteLine("*fin*");
            Console.ReadLine();
        }
    }

    public class ThingDeleterConfig
    {
        public GraphServiceClient GraphClient { get; set; }
        public int MaxPageSize { get; set; } = 999;
    }

    public class ThingDeleter
    {
        private readonly Option ConsistencyLevelHeaderOption = new HeaderOption("ConsistencyLevel", "eventual");
        private readonly Option CountQueryOption = new QueryOption("$count", "true");

        private readonly GraphServiceClient _graphServiceClient;
        private readonly int _maxPageSize;
        public ThingDeleter(ThingDeleterConfig config)
        {
            _graphServiceClient = config.GraphClient;
            _maxPageSize = config.MaxPageSize;
        }

        // delete all users from tenant
        [Obsolete("Use DeleteAllUsersFromTenant() instead", true)]
        public async Task DeleteAllUsersFromTenant2()
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
                    // hmm
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

        public async Task DeleteAllUsersFromTenant()
        {
            Console.WriteLine("Deleting users - getting your user id so we don't delete you");
            var me = await _graphServiceClient.Me.Request().Select(x => x.Id).GetAsync();
            var users = await _graphServiceClient.Users.Request(new[] { ConsistencyLevelHeaderOption, CountQueryOption })
                .Select(x => x.Id)
                .Top(5)
                .GetAsync();

            Console.WriteLine($"{users.CurrentPage.Count} users found, deleting them");

            await DeleteEntities(users, user =>
            {
                return _graphServiceClient.Users[user.Id].Request().RequestUrl;
            },
            user => user.Id == me.Id); // don't delete me
        }

        public async Task DeleteAllApplicationsFromTenant()
        {
            var apps = await _graphServiceClient.Applications.Request(new[] { ConsistencyLevelHeaderOption })
                .Select(x => x.Id)
                .Top(5)
            .GetAsync();

            await DeleteEntities(apps, (app) =>
            {
                return _graphServiceClient.Applications[app.Id].Request().RequestUrl;
            });
        }

        public async Task DeleteAllServicePrincipalsFromTenant()
        {
            var apps = await _graphServiceClient.ServicePrincipals.Request()
                .Select(x => x.Id)
                .Top(20)
            .GetAsync();

            // this is dumb. literally the only change is the entity path here
            await DeleteEntities(apps, (app) =>
            {
                return _graphServiceClient.ServicePrincipals[app.Id].Request().RequestUrl;
            });
        }

        public async Task DeleteEntities<T>(ICollectionPage<T> request, Func<T, string> deletionUrl, Func<T, bool> precheck = null) where T : DirectoryObject
        {
            Console.WriteLine("Entering DeleteEntities, building batch...");
            var totalSize = 0;
            var batch = new BatchRequestContent();
            var currentBatchStep = 1;
            var pageIterator = PageIterator<T>
            .CreatePageIterator(
                _graphServiceClient,
                request,
                (x) =>
                {
                    totalSize++;
                    Console.CursorLeft = 0;
                    Console.Write($"Processing {totalSize} of {request.AdditionalData["@odata.count"]}");
                    Console.CursorLeft = 0;
                    if (precheck != null && precheck(x)) return true;

                    var httpDeleteUrl = deletionUrl(x);

                    var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, httpDeleteUrl);
                    var requestStep = new BatchRequestStep(currentBatchStep.ToString(), deleteRequest, null);
                    batch.AddBatchRequestStep(requestStep);

                    if (currentBatchStep == request.Count)
                    {
                        //_graphServiceClient.Batch.Request().PostAsync(batch).GetAwaiter().GetResult();
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