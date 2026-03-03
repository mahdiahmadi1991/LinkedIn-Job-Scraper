using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace LinkedIn.JobScraper.Web.Tests.Infrastructure;

internal sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
