using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace K1Soft.IT.VectorTileHub.AspNetCore.Controllers;

/// <summary>
/// Prepends the host-configured <see cref="VectorTileHubOptions.RoutePrefix"/> to every
/// VectorTileHub controller. Controllers declare prefix-relative routes; this convention
/// supplies the (runtime-configurable) prefix that attribute routing cannot express statically.
/// Only controllers shipped by this library are affected — the host's own controllers are left alone.
/// </summary>
internal sealed class VectorTileHubRouteConvention : IApplicationModelConvention
{
    private static readonly System.Reflection.Assembly LibraryAssembly = typeof(VectorTileHubRouteConvention).Assembly;
    private readonly AttributeRouteModel _prefix;

    public VectorTileHubRouteConvention(string routePrefix)
    {
        var trimmed = (routePrefix ?? string.Empty).Trim('/');
        _prefix = new AttributeRouteModel(new RouteAttribute(trimmed));
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (controller.ControllerType.Assembly != LibraryAssembly)
            {
                continue;
            }

            foreach (var selector in controller.Selectors)
            {
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }
}
