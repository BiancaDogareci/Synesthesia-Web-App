using System;
using Synesthesia.Web.Models;
using Xunit;

namespace Synesthesia.Web.Tests;

public class BaseEntityTests
{
    [Fact]
    public void Constructor_SetsCreatedAtAndUpdatedAt()
    {
        var e = new BaseEntity();

        Assert.NotNull(e.CreatedAt);
        Assert.NotNull(e.UpdatedAt);

        Assert.True(e.CreatedAt <= e.UpdatedAt);
    }

    [Fact]
    public void Update_ChangesUpdatedAt()
    {
        var e = new BaseEntity();
        var before = e.UpdatedAt;

        System.Threading.Thread.Sleep(5);
        e.Update();

        Assert.NotNull(e.UpdatedAt);
        Assert.True(e.UpdatedAt > before);
    }
}
