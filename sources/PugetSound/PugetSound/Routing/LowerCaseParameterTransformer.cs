using Microsoft.AspNetCore.Routing;

namespace PugetSound.Routing
{
    public class LowerCaseParameterTransformer : IOutboundParameterTransformer
    {
        public string TransformOutbound(object value) => value?.ToString().ToLowerInvariant();
    }
}
