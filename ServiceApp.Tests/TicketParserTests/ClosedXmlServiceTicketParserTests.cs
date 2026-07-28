using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.TicketParser;
using ServiceApp.Tests.TestData;
using Xunit;

namespace ServiceApp.Tests.TicketParserTests;

public sealed class ClosedXmlServiceTicketParserTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ClosedXmlServiceTicketParser _parser;

    public ClosedXmlServiceTicketParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ServiceAppTicketTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        _parser = new ClosedXmlServiceTicketParser(NullLogger<ClosedXmlServiceTicketParser>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Parse_ValidWorkbook_ExtractsAllTicketsWithCauseAndDate()
    {
        var filePath = Path.Combine(_tempDirectory, "tickets.xlsx");
        var tickets = new[]
        {
            (TicketNumber: 1001, CreatedAt: new DateTime(2026, 3, 1, 9, 0, 0), Cause: (string?)"Elektrik Bauteil Defekt", Type: (string?)"Störung"),
            (TicketNumber: 1002, CreatedAt: new DateTime(2026, 3, 2, 14, 30, 0), Cause: (string?)"Mechanik Verschleiß", Type: (string?)"Anfrage")
        };
        TestExcelBuilder.CreateTicketWorkbook(filePath, tickets);

        var result = _parser.Parse(filePath);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.TicketNumber == 1001 && t.Cause == "Elektrik Bauteil Defekt" && t.Type == "Störung" && t.CreatedAt == new DateTime(2026, 3, 1, 9, 0, 0));
        Assert.Contains(result, t => t.TicketNumber == 1002 && t.Cause == "Mechanik Verschleiß" && t.Type == "Anfrage");
    }

    [Fact]
    public void Parse_EmptyCauseCell_ResultsInNullCause()
    {
        var filePath = Path.Combine(_tempDirectory, "tickets.xlsx");
        TestExcelBuilder.CreateTicketWorkbook(filePath, new[]
        {
            (TicketNumber: 1, CreatedAt: new DateTime(2026, 1, 1), Cause: (string?)null, Type: (string?)null)
        });

        var result = _parser.Parse(filePath);

        var ticket = Assert.Single(result);
        Assert.Null(ticket.Cause);
        Assert.Null(ticket.Type);
    }

    [Fact]
    public void Parse_ColumnOrderDiffersFromSample_StillFindsColumnsByHeaderName()
    {
        // TestExcelBuilder legt die Spalten bewusst in anderer Reihenfolge an als die
        // Beispieldatei (Titel, Ticketnummer, Fehlercode Ursache, Anlagedatum, Typ) -
        // der Parser muss trotzdem anhand der Kopfzeile die richtigen Spalten finden.
        var filePath = Path.Combine(_tempDirectory, "tickets.xlsx");
        TestExcelBuilder.CreateTicketWorkbook(filePath, new[]
        {
            (TicketNumber: 42, CreatedAt: new DateTime(2026, 5, 5), Cause: (string?)"Kunde Wartung", Type: (string?)"Reklamation")
        });

        var result = _parser.Parse(filePath);

        var ticket = Assert.Single(result);
        Assert.Equal(42, ticket.TicketNumber);
        Assert.Equal("Kunde Wartung", ticket.Cause);
        Assert.Equal("Reklamation", ticket.Type);
        Assert.Equal(new DateTime(2026, 5, 5), ticket.CreatedAt);
    }

    [Fact]
    public void Parse_WorkbookWithDetailColumns_ExtractsFieldsForDrillDown()
    {
        var filePath = Path.Combine(_tempDirectory, "tickets_full.xlsx");
        TestExcelBuilder.CreateFullTicketWorkbook(filePath, new[]
        {
            (TicketNumber: 2001, CreatedAt: new DateTime(2026, 4, 1), Cause: (string?)"Elektrik", Type: (string?)"Störung",
                AddressLine: (string?)"Musterstraße 1, 12345 Musterstadt", ErrorLocation: (string?)"Keller",
                ErrorFix: (string?)"Sicherung getauscht", InternalStatus: (string?)"Erledigt", Responsible: (string?)"Max Mustermann")
        });

        var result = _parser.Parse(filePath);

        var ticket = Assert.Single(result);
        Assert.Equal("Musterstraße 1, 12345 Musterstadt", ticket.AddressLine);
        Assert.Equal("Keller", ticket.ErrorLocation);
        Assert.Equal("Sicherung getauscht", ticket.ErrorFix);
        Assert.Equal("Erledigt", ticket.InternalStatus);
        Assert.Equal("Max Mustermann", ticket.Responsible);
    }

    [Fact]
    public void Parse_WorkbookWithoutDetailColumns_LeavesDetailFieldsNull()
    {
        var filePath = Path.Combine(_tempDirectory, "tickets_no_details.xlsx");
        TestExcelBuilder.CreateTicketWorkbook(filePath, new[]
        {
            (TicketNumber: 1, CreatedAt: new DateTime(2026, 1, 1), Cause: (string?)"Elektrik", Type: (string?)"Störung")
        });

        var result = _parser.Parse(filePath);

        var ticket = Assert.Single(result);
        Assert.Null(ticket.AddressLine);
        Assert.Null(ticket.ErrorLocation);
        Assert.Null(ticket.ErrorFix);
        Assert.Null(ticket.InternalStatus);
        Assert.Null(ticket.Responsible);
    }

    [Fact]
    public void Parse_FileDoesNotExist_ThrowsTicketFileNotFoundException()
    {
        var filePath = Path.Combine(_tempDirectory, "missing.xlsx");

        Assert.Throws<TicketFileNotFoundException>(() => _parser.Parse(filePath));
    }

    [Fact]
    public void Parse_CorruptFile_ThrowsCorruptTicketFileException()
    {
        var filePath = Path.Combine(_tempDirectory, "corrupt.xlsx");
        TestExcelBuilder.CreateCorruptWorkbook(filePath);

        Assert.Throws<CorruptTicketFileException>(() => _parser.Parse(filePath));
    }

    [Fact]
    public void Parse_MissingRequiredColumns_ThrowsInvalidTicketFileFormatException()
    {
        var filePath = Path.Combine(_tempDirectory, "no_columns.xlsx");
        TestExcelBuilder.CreateWorkbookMissingRequiredColumns(filePath);

        Assert.Throws<InvalidTicketFileFormatException>(() => _parser.Parse(filePath));
    }
}
