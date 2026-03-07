using Application.Common.Models;
using FluentAssertions;

namespace Tests.Domain;

public class PagedResultTests
{
    [Fact]
    public void TotalPages_ExactDivision_ReturnsCorrectCount()
    {
        var result = new PagedResult<string>(["a", "b"], 10, 1, 5);

        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public void TotalPages_PartialLastPage_RoundsUp()
    {
        var result = new PagedResult<string>(["a"], 11, 1, 5);

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void TotalPages_SingleItem_ReturnsOne()
    {
        var result = new PagedResult<string>(["a"], 1, 1, 10);

        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void TotalPages_ZeroItems_ReturnsZero()
    {
        var result = new PagedResult<string>([], 0, 1, 10);

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void Properties_ReturnConstructorValues()
    {
        var items = new List<string> { "a", "b" };
        var result = new PagedResult<string>(items, 50, 3, 10);

        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(50);
        result.Page.Should().Be(3);
        result.PageSize.Should().Be(10);
    }
}
