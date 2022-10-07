# tenant-deleter

Azure AD requires tenants to be largely empty of objects before the tenant can be deleted. This tool helps bulk-delete certain resources. Supports the following resource types:
- Users
- Applications/App Registrations
- Service Principals/Enterprise Apps

## A note on speed & throttling

This uses Microsoft Graph, which has a variety of throttles across different facets of the service - application, tenant, etc. Read more here: https://docs.microsoft.com/en-us/graph/throttling

AAD object throttling information is here: https://learn.microsoft.com/en-us/graph/throttling-limits#identity-and-access-service-limits

This tool attempts to maximize efficiency by using
- Deletion requests are batched - default batch size is 20, but can be configured. 20 is the maximum batch size allowed by Graph.
- `$select` - only the `id` property is requested for each object, which is all that is needed for deletion.
- `$top` - the maximum number of objects to return in a single request. Default is 999, which is the maximum allowed by Graph.
- Uses Graph SDK for requests, which handles retries and backoff.

## Usage

`dotnet run -- your-tenant-id` or `tenant-deleter your-tenant-id`

## Precompiled binaries

Available on releases page for win-x64, osx-arm64 and linux-x64.

## todo

- [ ] Add support for more resource types
- [ ] Extract app configuration 
- [ ] Wrap as `IHostedService` to push tasks to background