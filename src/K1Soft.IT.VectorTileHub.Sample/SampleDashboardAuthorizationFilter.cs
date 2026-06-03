using Hangfire.Dashboard;

namespace K1Soft.IT.VectorTileHub.Sample;

/// <summary>
/// Host-supplied authorization for the Hangfire dashboard. The library mounts the dashboard but
/// applies no policy of its own (its default permits local requests only); the host passes this
/// filter to <c>UseVectorTileHubHangfireDashboard(...)</c> to restrict access to the GISAdmin role.
/// </summary>
public sealed class SampleDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User?.IsInRole("GISAdmin") == true;
    }
}
