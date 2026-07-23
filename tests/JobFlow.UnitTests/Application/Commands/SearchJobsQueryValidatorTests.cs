using FluentAssertions;
using FluentValidation.TestHelper;
using JobFlow.Application.Queries.SearchJobs;
using JobFlow.Application.Validators;

namespace JobFlow.UnitTests.Application.Commands;

public class SearchJobsQueryValidatorTests
{
    private readonly SearchJobsQueryValidator _validator = new();

    [Fact]
    public void Valid_Query_Should_Pass()
    {
        var query = new SearchJobsQuery(null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Page_Below_One_Should_Fail()
    {
        var query = new SearchJobsQuery(null, null, null, null, Page: 0);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void PageSize_Above_100_Should_Fail()
    {
        var query = new SearchJobsQuery(null, null, null, null, PageSize: 101);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void PageSize_Below_One_Should_Fail()
    {
        var query = new SearchJobsQuery(null, null, null, null, PageSize: 0);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Invalid_SortOrder_Should_Fail()
    {
        var query = new SearchJobsQuery(null, null, null, null, SortOrder: "invalid");
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void Asc_SortOrder_Should_Pass()
    {
        var query = new SearchJobsQuery(null, null, null, null, SortOrder: "asc");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortOrder);
    }

    [Fact]
    public void Desc_SortOrder_Should_Pass()
    {
        var query = new SearchJobsQuery(null, null, null, null, SortOrder: "desc");
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SortOrder);
    }
}
