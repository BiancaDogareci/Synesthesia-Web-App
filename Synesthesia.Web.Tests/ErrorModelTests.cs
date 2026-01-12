using Microsoft.Extensions.Logging;
using Moq;
using Synesthesia.Web.Pages;
using Xunit;

namespace Synesthesia.Web.Tests;

public class ErrorModelTests
{
    [Fact]
    public void OnGet_SetsRequestId_And_ShowRequestIdTrue()
    {
        var logger = new Mock<ILogger<ErrorModel>>();
        var model = new ErrorModel(logger.Object);

        var (_, pc) = TestHelpers.BuildPageContext();
        model.PageContext = pc;

        model.OnGet();

        Assert.False(string.IsNullOrWhiteSpace(model.RequestId));
        Assert.True(model.ShowRequestId);
    }
}
