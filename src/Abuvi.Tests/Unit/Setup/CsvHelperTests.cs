using FluentAssertions;
using Abuvi.Setup;

namespace Abuvi.Tests.Unit.Setup;

public class CsvHelperTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string CreateTempCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // --- Parse ---

    [Fact]
    public void Parse_WithValidFile_ReturnsCorrectRowCount()
    {
        var path = CreateTempCsv("email,name\nfoo@bar.com,Foo\nbaz@bar.com,Baz\nqux@bar.com,Qux");

        var rows = CsvHelper.Parse(path);

        rows.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_WithEmptyFile_ReturnsEmptyList()
    {
        var path = CreateTempCsv("");

        var rows = CsvHelper.Parse(path);

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithHeaderOnly_ReturnsEmptyList()
    {
        var path = CreateTempCsv("email,name");

        var rows = CsvHelper.Parse(path);

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithExtraWhitespace_TrimsAllFields()
    {
        var path = CreateTempCsv(" email , name \n foo@bar.com , Foo ");

        var rows = CsvHelper.Parse(path);

        rows.Should().HaveCount(1);
        rows[0].Should().ContainKey("email");
        rows[0]["email"].Should().Be("foo@bar.com");
        rows[0]["name"].Should().Be("Foo");
    }

    [Fact]
    public void Parse_HeadersAreCaseInsensitive()
    {
        var path = CreateTempCsv("Email,Name\nfoo@bar.com,Foo");

        var rows = CsvHelper.Parse(path);

        rows[0]["email"].Should().Be("foo@bar.com");
        rows[0]["EMAIL"].Should().Be("foo@bar.com");
        rows[0]["Email"].Should().Be("foo@bar.com");
    }

    // --- Require ---

    [Fact]
    public void Require_WithExistingKey_ReturnsValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = "foo@bar.com"
        };

        CsvHelper.Require(row, "email").Should().Be("foo@bar.com");
    }

    [Fact]
    public void Require_WithMissingKey_ThrowsInvalidOperationException()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var act = () => CsvHelper.Require(row, "email");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*email*");
    }

    [Fact]
    public void Require_WithEmptyValue_ThrowsInvalidOperationException()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["email"] = "  "
        };

        var act = () => CsvHelper.Require(row, "email");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*email*");
    }

    // --- Optional ---

    [Fact]
    public void Optional_WithExistingKey_ReturnsValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["phone"] = "+34612345678"
        };

        CsvHelper.Optional(row, "phone").Should().Be("+34612345678");
    }

    [Fact]
    public void Optional_WithMissingKey_ReturnsNull()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        CsvHelper.Optional(row, "phone").Should().BeNull();
    }

    [Fact]
    public void Optional_WithEmptyValue_ReturnsNull()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["phone"] = ""
        };

        CsvHelper.Optional(row, "phone").Should().BeNull();
    }

    // --- RequireDecimal ---

    [Fact]
    public void RequireDecimal_WithValidDecimal_ReturnsParsedValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["price"] = "150.00"
        };

        CsvHelper.RequireDecimal(row, "price").Should().Be(150.00m);
    }

    [Fact]
    public void RequireDecimal_WithInvalidValue_ThrowsException()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["price"] = "not-a-number"
        };

        var act = () => CsvHelper.RequireDecimal(row, "price");

        act.Should().Throw<FormatException>();
    }

    // --- OptionalInt ---

    [Fact]
    public void OptionalInt_WithValidInt_ReturnsParsedValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxCapacity"] = "100"
        };

        CsvHelper.OptionalInt(row, "maxCapacity").Should().Be(100);
    }

    [Fact]
    public void OptionalInt_WithEmptyValue_ReturnsNull()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxCapacity"] = ""
        };

        CsvHelper.OptionalInt(row, "maxCapacity").Should().BeNull();
    }
}
