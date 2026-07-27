using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;
using ServiceApp.Core.PdfParser;
using ServiceApp.Tests.TestData;
using Xunit;

namespace ServiceApp.Tests.PdfParserTests;

public sealed class PdfPigReportParserTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PdfPigReportParser _parser;

    public PdfPigReportParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ServiceAppTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);

        var interpreters = new IReportLineInterpreter[]
        {
            new GermanTableLineInterpreter(),
            new IsoTableLineInterpreter()
        };

        _parser = new PdfPigReportParser(interpreters, NullLogger<PdfPigReportParser>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Parse_GermanFormatReport_ExtractsAllCallsWithCorrectStatusAndDuration()
    {
        var filePath = Path.Combine(_tempDirectory, "german.pdf");
        var calls = new[]
        {
            (Timestamp: new DateTime(2026, 7, 27, 9, 15, 32), Answered: true, Duration: (TimeSpan?)TimeSpan.FromSeconds(192)),
            (Timestamp: new DateTime(2026, 7, 27, 10, 0, 0), Answered: false, Duration: (TimeSpan?)null)
        };
        TestPdfBuilder.CreateGermanFormatReport(filePath, calls);

        var result = _parser.Parse(filePath, "service-1");

        Assert.Equal(2, result.Count);

        var answered = Assert.Single(result, r => r.IsAnswered);
        Assert.Equal(new DateTime(2026, 7, 27, 9, 15, 32), answered.Timestamp);
        Assert.Equal(192, answered.DurationSeconds);
        Assert.Equal("service-1", answered.ServiceNumberId);
        Assert.Equal("german.pdf", answered.SourceFile);

        var missed = Assert.Single(result, r => !r.IsAnswered);
        Assert.Equal(new DateTime(2026, 7, 27, 10, 0, 0), missed.Timestamp);
        Assert.Null(missed.DurationSeconds);
    }

    [Fact]
    public void Parse_IsoFormatReport_ExtractsAllCalls()
    {
        var filePath = Path.Combine(_tempDirectory, "iso.pdf");
        var calls = new[]
        {
            (Timestamp: new DateTime(2026, 3, 1, 14, 30, 0), Answered: true, Duration: (TimeSpan?)TimeSpan.FromMinutes(5)),
            (Timestamp: new DateTime(2026, 3, 2, 8, 5, 0), Answered: false, Duration: (TimeSpan?)null)
        };
        TestPdfBuilder.CreateIsoFormatReport(filePath, calls);

        var result = _parser.Parse(filePath, "service-2");

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("service-2", r.ServiceNumberId));
        Assert.Contains(result, r => r.IsAnswered && r.DurationSeconds == 300);
        Assert.Contains(result, r => !r.IsAnswered);
    }

    [Fact]
    public void Parse_FileDoesNotExist_ThrowsPdfNotFoundException()
    {
        var filePath = Path.Combine(_tempDirectory, "missing.pdf");

        Assert.Throws<PdfNotFoundException>(() => _parser.Parse(filePath, "service-1"));
    }

    [Fact]
    public void Parse_CorruptPdf_ThrowsCorruptPdfException()
    {
        var filePath = Path.Combine(_tempDirectory, "corrupt.pdf");
        TestPdfBuilder.CreateCorruptPdf(filePath);

        Assert.Throws<CorruptPdfException>(() => _parser.Parse(filePath, "service-1"));
    }

    [Fact]
    public void Parse_UnrecognizedFormat_ThrowsUnrecognizedReportFormatException()
    {
        var filePath = Path.Combine(_tempDirectory, "unknown.pdf");
        TestPdfBuilder.CreateUnrecognizedFormatReport(filePath);

        Assert.Throws<UnrecognizedReportFormatException>(() => _parser.Parse(filePath, "service-1"));
    }
}
