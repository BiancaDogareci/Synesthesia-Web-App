using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Synesthesia.Web.Data;

namespace Synesthesia.Web.Tests;

public static class TestHelpers
{
    public static ApplicationDbContext CreateInMemoryDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(opts);
    }

    public static ClaimsPrincipal BuildUser(string userId, string? userName = "testuser", bool authenticated = true)
    {
        if (!authenticated)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName ?? "testuser")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    public static (DefaultHttpContext HttpContext, PageContext PageContext) BuildPageContext(ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null) httpContext.User = user;

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        return (httpContext, pageContext);
    }

    public static ITempDataDictionary BuildTempData(HttpContext httpContext)
    {
        // In-memory TempData provider for tests
        var provider = new InMemoryTempDataProvider();
        return new TempDataDictionary(httpContext, provider);
    }

    public static T GetProp<T>(object obj, string propName)
    {
        if (obj is null) throw new ArgumentNullException(nameof(obj));

        // If it comes through as JsonElement (can happen sometimes), handle it too
        if (obj is System.Text.Json.JsonElement je)
        {
            if (typeof(T) == typeof(bool)) return (T)(object)je.GetProperty(propName).GetBoolean();
            if (typeof(T) == typeof(string)) return (T)(object)je.GetProperty(propName).GetString()!;
            if (typeof(T) == typeof(Guid)) return (T)(object)je.GetProperty(propName).GetGuid();
            if (typeof(T) == typeof(int)) return (T)(object)je.GetProperty(propName).GetInt32();
            if (typeof(T) == typeof(double)) return (T)(object)je.GetProperty(propName).GetDouble();

            var raw = je.GetProperty(propName).GetRawText();
            return System.Text.Json.JsonSerializer.Deserialize<T>(raw)!;
        }

        // Anonymous type / POCO via reflection
        var prop = obj.GetType().GetProperty(propName);
        if (prop == null)
            throw new InvalidOperationException(
                $"Property '{propName}' not found on type '{obj.GetType().FullName}'.");

        return (T)prop.GetValue(obj)!;
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private const string Key = "__TEST_TEMPDATA__";

        public IDictionary<string, object?> LoadTempData(HttpContext context)
        {
            if (context.Items.TryGetValue(Key, out var existing) &&
                existing is IDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict);
            }

            return new Dictionary<string, object?>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            context.Items[Key] = new Dictionary<string, object?>(values);
        }
    }
}
