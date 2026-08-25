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

    // --- Parse: quoted fields (RFC 4180) ---

    [Fact]
    public void Parse_WithQuotedHeaders_StripsQuotesFromKeys()
    {
        var path = CreateTempCsv("\"id\",\"name\",\"location\"\n\"1\",\"Selva de Oza\",\"Huesca\"");

        var rows = CsvHelper.Parse(path);

        rows.Should().HaveCount(1);
        rows[0].Should().ContainKey("name");
        rows[0]["name"].Should().Be("Selva de Oza");
    }

    [Fact]
    public void Parse_WithQuotedValues_StripsSurroundingQuotes()
    {
        var path = CreateTempCsv("name,pricePerAdult\n\"Selva de Oza\",\"0\"");

        var rows = CsvHelper.Parse(path);

        rows[0]["name"].Should().Be("Selva de Oza");
        rows[0]["pricePerAdult"].Should().Be("0");
    }

    [Fact]
    public void Parse_WithCommaInsideQuotedField_KeepsFieldIntact()
    {
        // Caso real: docs/CAMPAMENTOS_HISTORICOS.csv
        var path = CreateTempCsv("\"name\",\"location\"\n\"Palacio de las Teyerias, Mestas de Con\",\"Asturias\"");

        var rows = CsvHelper.Parse(path);

        rows.Should().HaveCount(1);
        rows[0]["name"].Should().Be("Palacio de las Teyerias, Mestas de Con");
        rows[0]["location"].Should().Be("Asturias");
    }

    [Fact]
    public void Parse_WithEscapedDoubleQuotes_UnescapesThem()
    {
        var path = CreateTempCsv("\"name\",\"notes\"\n\"Campamento \"\"San Juan\"\"\",\"ok\"");

        var rows = CsvHelper.Parse(path);

        rows[0]["name"].Should().Be("Campamento \"San Juan\"");
        rows[0]["notes"].Should().Be("ok");
    }

    [Fact]
    public void Parse_WithMixedQuotedAndUnquotedFields_HandlesBoth()
    {
        var path = CreateTempCsv("name,location,price\nOto,\"Huesca\",0");

        var rows = CsvHelper.Parse(path);

        rows[0]["name"].Should().Be("Oto");
        rows[0]["location"].Should().Be("Huesca");
        rows[0]["price"].Should().Be("0");
    }

    [Fact]
    public void Parse_WithEmptyQuotedField_ReturnsEmptyString()
    {
        var path = CreateTempCsv("name,description\n\"Oto\",\"\"");

        var rows = CsvHelper.Parse(path);

        rows[0]["description"].Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithUnquotedFile_StillWorks()
    {
        // Regresion: los seed CSV existentes no llevan comillas
        var path = CreateTempCsv("name,description,location\nCamp Sierra,Annual camp,Sierra de Guadarrama");

        var rows = CsvHelper.Parse(path);

        rows.Should().HaveCount(1);
        rows[0]["name"].Should().Be("Camp Sierra");
        rows[0]["location"].Should().Be("Sierra de Guadarrama");
    }

    [Fact]
    public void Parse_TrimsWhitespaceOutsideQuotesButPreservesItInside()
    {
        var path = CreateTempCsv("name,location\n  Oto  ,\"  Huesca  \"");

        var rows = CsvHelper.Parse(path);

        rows[0]["name"].Should().Be("Oto");
        rows[0]["location"].Should().Be("  Huesca  ");
    }

    // --- OptionalDecimal ---

    [Fact]
    public void OptionalDecimal_WithValidValue_ReturnsParsedValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["latitude"] = "42.7833"
        };

        CsvHelper.OptionalDecimal(row, "latitude").Should().Be(42.7833m);
    }

    [Fact]
    public void OptionalDecimal_WithNegativeValue_ReturnsParsedValue()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["longitude"] = "-0.6833"
        };

        CsvHelper.OptionalDecimal(row, "longitude").Should().Be(-0.6833m);
    }

    [Fact]
    public void OptionalDecimal_WithEmptyValue_ReturnsNull()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["latitude"] = ""
        };

        CsvHelper.OptionalDecimal(row, "latitude").Should().BeNull();
    }

    [Fact]
    public void OptionalDecimal_WithMissingKey_ReturnsNull()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        CsvHelper.OptionalDecimal(row, "latitude").Should().BeNull();
    }

    [Fact]
    public void OptionalDecimal_UsesInvariantCulture()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["latitude"] = "40.5"
        };

        // Con cultura es-ES la coma seria el separador; debe seguir siendo el punto
        CsvHelper.OptionalDecimal(row, "latitude").Should().Be(40.5m);
    }

    // --- Write (round-trip) ---

    [Fact]
    public void Write_ThenParse_RoundTripsPlainValues()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["name"] = "Oto", ["location"] = "Huesca" }
        };

        CsvHelper.Write(path, ["name", "location"], rows);
        var back = CsvHelper.Parse(path);

        back.Should().HaveCount(1);
        back[0]["name"].Should().Be("Oto");
        back[0]["location"].Should().Be("Huesca");
    }

    [Fact]
    public void Write_ThenParse_RoundTripsCommasAndQuotes()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = "Palacio de las Teyerias, Mestas de Con",
                ["notes"] = "dijo \"si\" al campamento"
            }
        };

        CsvHelper.Write(path, ["name", "notes"], rows);
        var back = CsvHelper.Parse(path);

        back[0]["name"].Should().Be("Palacio de las Teyerias, Mestas de Con");
        back[0]["notes"].Should().Be("dijo \"si\" al campamento");
    }

    [Fact]
    public void Write_WithMissingKey_WritesEmptyField()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["name"] = "Oto" }
        };

        CsvHelper.Write(path, ["name", "latitude"], rows);
        var back = CsvHelper.Parse(path);

        back[0]["latitude"].Should().BeEmpty();
    }

    [Fact]
    public void Write_PreservesColumnOrder()
    {
        var path = Path.GetTempFileName();
        _tempFiles.Add(path);
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["b"] = "2", ["a"] = "1" }
        };

        CsvHelper.Write(path, ["a", "b"], rows);

        File.ReadAllLines(path)[0].Should().Be("\"a\",\"b\"");
    }
}
