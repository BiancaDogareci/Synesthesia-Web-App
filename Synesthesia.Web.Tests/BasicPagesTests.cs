using Microsoft.Extensions.Logging;
using Moq;
using Synesthesia.Web.Pages;
using Xunit;

namespace Synesthesia.Web.Tests;

public class BasicPagesTests
{
    [Fact]
    public void IndexModel_OnGet_DoesNotThrow()
    {
        var logger = new Mock<ILogger<IndexModel>>();
        var model = new IndexModel(logger.Object);

        model.OnGet();
    }

    [Fact]
    public void PrivacyModel_OnGet_DoesNotThrow()
    {
        var logger = new Mock<ILogger<PrivacyModel>>();
        var model = new PrivacyModel(logger.Object);

        model.OnGet();
    }
}
