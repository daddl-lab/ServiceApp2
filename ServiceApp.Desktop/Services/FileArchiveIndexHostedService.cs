using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Configuration;
using ServiceApp.Core.Exceptions;
using ServiceApp.Core.FileArchive;

namespace ServiceApp.Desktop.Services;

/// <summary>
/// Hintergrunddienst, der den Zeichnungsarchiv-Index aktuell hält: baut ihn beim ersten
/// Start (bzw. wenn er fehlt oder beschädigt war) vollständig auf und aktualisiert ihn
/// danach in dem in den Einstellungen konfigurierten Intervall inkrementell. Läuft über
/// den .NET Generic Host (siehe <c>Program.cs</c>), unabhängig von der Avalonia-Oberfläche.
///
/// Liest Archivpfad und Intervall bei jedem Zyklus frisch aus den Einstellungen, damit
/// Änderungen (z. B. ein neu konfigurierter Archivpfad) ohne Neustart der Anwendung
/// wirksam werden. Ein manuelles "Index jetzt aktualisieren" aus den Einstellungen ruft
/// denselben <see cref="IFileArchiveIndexService"/> direkt auf - der dortige
/// Überlappungsschutz stellt sicher, dass sich beide niemals in die Quere kommen.
/// </summary>
public sealed class FileArchiveIndexHostedService : BackgroundService
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan UnreachableRetryInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NoArchivePathPollInterval = TimeSpan.FromSeconds(30);

    private readonly ISettingsService _settingsService;
    private readonly IFileArchiveIndexStore _indexStore;
    private readonly IFileArchiveIndexService _indexService;
    private readonly ILogger<FileArchiveIndexHostedService> _logger;

    public FileArchiveIndexHostedService(
        ISettingsService settingsService,
        IFileArchiveIndexStore indexStore,
        IFileArchiveIndexService indexService,
        ILogger<FileArchiveIndexHostedService> logger)
    {
        _settingsService = settingsService;
        _indexStore = indexStore;
        _indexService = indexService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _indexStore.Initialize();
        }
        catch (IndexCorruptedException ex)
        {
            _logger.LogError(ex, "Suchindex konnte auch nach dem Versuch eines Neuaufbaus nicht initialisiert werden.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsService.Load();
            var waitInterval = TimeSpan.FromMinutes(Math.Max(MinimumInterval.TotalMinutes, settings.FileArchiveUpdateIntervalMinutes));

            if (string.IsNullOrWhiteSpace(settings.DrawingArchivePath))
            {
                waitInterval = NoArchivePathPollInterval;
            }
            else
            {
                try
                {
                    if (!_indexService.GetStatus().IndexReady)
                    {
                        await _indexService.RunFullScanAsync(settings.DrawingArchivePath, progress: null, stoppingToken).ConfigureAwait(false);
                    }
                    else if (settings.FileArchiveAutoUpdateEnabled)
                    {
                        await _indexService.RunIncrementalScanAsync(settings.DrawingArchivePath, progress: null, stoppingToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ArchiveUnreachableException ex)
                {
                    _logger.LogWarning(ex, "Archiv nicht erreichbar, erneuter Versuch in {Minutes} Minuten.", UnreachableRetryInterval.TotalMinutes);
                    waitInterval = UnreachableRetryInterval;
                }
                catch (ArchiveRootNotFoundException ex)
                {
                    _logger.LogWarning(ex, "Archivpfad nicht gefunden, erneuter Versuch in {Minutes} Minuten.", UnreachableRetryInterval.TotalMinutes);
                    waitInterval = UnreachableRetryInterval;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Automatische Indexaktualisierung ist fehlgeschlagen.");
                }
            }

            try
            {
                await Task.Delay(waitInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
