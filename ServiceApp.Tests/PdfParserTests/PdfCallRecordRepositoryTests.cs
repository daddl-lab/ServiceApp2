using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.Models;
using ServiceApp.Core.PdfParser;
using ServiceApp.Core.Repository;
using ServiceApp.Tests.TestData;
using Xunit;

namespace ServiceApp.Tests.PdfParserTests;

public sealed class PdfCallRecordRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PdfCallRecordRepository _repository;

    public PdfCallRecordRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ServiceAppRepoTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);

        var parser = new PdfPigReportParser(
            new IReportLineInterpreter[] { new GermanTableLineInterpreter(), new IsoTableLineInterpreter() },
            NullLogger<PdfPigReportParser>.Instance);

        _repository = new PdfCallRecordRepository(parser, NullLogger<PdfCallRecordRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetCallRecords_MultiplePdfsInFolder_AggregatesAllRecords()
    {
        TestPdfBuilder.CreateGermanFormatReport(Path.Combine(_tempDirectory, "week1.pdf"), new[]
        {
            (new DateTime(2026, 7, 20, 9, 0, 0), true, (TimeSpan?)TimeSpan.FromMinutes(2))
        });
        TestPdfBuilder.CreateGermanFormatReport(Path.Combine(_tempDirectory, "week2.pdf"), new[]
        {
            (new DateTime(2026, 7, 21, 10, 0, 0), false, (TimeSpan?)null)
        });

        var settings = new ServiceNumberSettings { Id = "service-1", Name = "Test", PdfFolderPath = _tempDirectory };
        var result = _repository.GetCallRecords(settings);

        Assert.Equal(2, result.Records.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void GetCallRecords_OneCorruptFileAmongValidOnes_SkipsCorruptFileAndReportsWarning()
    {
        TestPdfBuilder.CreateGermanFormatReport(Path.Combine(_tempDirectory, "valid.pdf"), new[]
        {
            (new DateTime(2026, 7, 20, 9, 0, 0), true, (TimeSpan?)null)
        });
        TestPdfBuilder.CreateCorruptPdf(Path.Combine(_tempDirectory, "broken.pdf"));

        var settings = new ServiceNumberSettings { Id = "service-1", Name = "Test", PdfFolderPath = _tempDirectory };
        var result = _repository.GetCallRecords(settings);

        Assert.Single(result.Records);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("broken.pdf", warning.FileName);
    }

    [Fact]
    public void GetCallRecords_EmptyFolder_ReturnsEmptyResultWithoutError()
    {
        var settings = new ServiceNumberSettings { Id = "service-1", Name = "Test", PdfFolderPath = _tempDirectory };
        var result = _repository.GetCallRecords(settings);

        Assert.Empty(result.Records);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void GetCallRecords_FolderDoesNotExist_ThrowsPdfFolderNotFoundException()
    {
        var settings = new ServiceNumberSettings
        {
            Id = "service-1",
            Name = "Test",
            PdfFolderPath = Path.Combine(_tempDirectory, "does-not-exist")
        };

        Assert.Throws<PdfFolderNotFoundException>(() => _repository.GetCallRecords(settings));
    }
}
