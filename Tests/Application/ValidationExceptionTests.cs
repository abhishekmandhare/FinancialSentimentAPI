using FluentAssertions;
using FluentValidation.Results;
using ValidationException = Application.Exceptions.ValidationException;

namespace Tests.Application;

public class ValidationExceptionTests
{
    [Fact]
    public void Constructor_GroupsErrorsByPropertyName()
    {
        var failures = new List<ValidationFailure>
        {
            new("Symbol", "Symbol is required."),
            new("Symbol", "Symbol must not exceed 10 chars."),
            new("Text", "Text is required."),
        };

        var ex = new ValidationException(failures);

        ex.Errors.Should().ContainKey("Symbol");
        ex.Errors["Symbol"].Should().HaveCount(2);
        ex.Errors.Should().ContainKey("Text");
        ex.Errors["Text"].Should().ContainSingle();
    }

    [Fact]
    public void Constructor_EmptyFailures_ProducesEmptyErrors()
    {
        var ex = new ValidationException([]);
        ex.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Message_ContainsValidationText()
    {
        var ex = new ValidationException([]);
        ex.Message.Should().Contain("validation");
    }
}
