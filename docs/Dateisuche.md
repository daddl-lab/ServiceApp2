# Zeichnungsarchiv-Dateisuche

Dokumentation der Dateisuche-Funktion in ServiceApp2: schnelle Suche über ein
Netzlaufwerk mit einem Konstruktions-Zeichnungsarchiv, inklusive lokalem Suchindex,
Platzhalter-Suchsyntax und Dateityp-Filter.

## Inhalt

1. [Verwendete Technologie](#1-verwendete-technologie)
2. [Architekturübersicht](#2-architekturübersicht)
3. [Warum diese Suchstrategie schnell ist](#3-warum-diese-suchstrategie-schnell-ist)
4. [Installation](#4-installation)
5. [Konfiguration des Netzlaufwerks](#5-konfiguration-des-netzlaufwerks)
6. [Aufbau und Aktualisierung des Suchindex](#6-aufbau-und-aktualisierung-des-suchindex)
7. [Suchsyntax](#7-suchsyntax)
8. [Unterstützte Dateitypen](#8-unterstützte-dateitypen)
9. [Start der Anwendung](#9-start-der-anwendung)
10. [Durchführung einer Indexaktualisierung](#10-durchführung-einer-indexaktualisierung)
11. [Fehlerbehebung](#11-fehlerbehebung)
12. [Bekannte Einschränkungen](#12-bekannte-einschränkungen)
13. [Performance-Messwerte](#13-performance-messwerte)

## 1. Verwendete Technologie

- **.NET 8 / Avalonia UI 11** – bestehendes Desktop-Framework der Anwendung, plattformunabhängig, mit nativem UNC-Pfad-Zugriff über die Standard-`System.IO`-APIs.
- **CommunityToolkit.Mvvm** – MVVM-Muster (ObservableObject/RelayCommand), wie im Rest der Anwendung.
- **Microsoft.Data.Sqlite** – eingebettete, dateibasierte Datenbank für den lokalen Suchindex. Neu für dieses Feature (erste Datenbankabhängigkeit der Anwendung), begründet durch die Anforderung an transaktionale Aktualisierbarkeit, Absturzsicherheit (WAL-Journaling) und automatische Selbstheilung bei Beschädigung – Funktionen, die eine einfache Datei "von Hand" nicht mit vertretbarem Aufwand bieten würde.
- **Microsoft.Extensions.Hosting** (bereits vorhanden) – der Suchindex wird als `BackgroundService` im ohnehin schon vorhandenen .NET Generic Host betrieben.
- **Avalonia.Controls.DataGrid** (bereits vorhanden) – virtualisierte, sortierbare Ergebnistabelle.

## 2. Architekturübersicht

```
Netzlaufwerk (\\SERVER\Zeichnungsarchiv)
        │
        ▼
FileArchiveIndexHostedService (Hintergrunddienst)
        │  Vollscan beim ersten Start, danach periodischer Inkrementalscan
        ▼
FileArchiveIndexService  ──isoliert Fehler pro Datei/Ordner──▶  IFileSystemWalker
        │
        ▼
FileArchiveIndexStore (lokale SQLite-Datenbank, Mark-and-Sweep, Selbstheilung)
        │  In-Memory-Snapshot laden
        ▼
FileArchiveSearchService
   ├─ Index bereit      → Suche gegen In-Memory-Snapshot (kein Netzlaufwerk-Zugriff)
   └─ Index nicht bereit → ArchivePathResolver grenzt Verzeichnis ein → schmaler Live-Scan
        ▼
FileSearchViewModel  ──streamt Treffer fortlaufend──▶  DataGrid (virtualisiert, sortierbar)
```

Zentrale Bausteine (alle in `ServiceApp.Core/FileArchive/`, mit Interface + Implementierung, wie in der übrigen Anwendung üblich):

| Klasse | Aufgabe |
|---|---|
| `WildcardPattern` | Übersetzt `*`/`?`-Suchbegriffe eindeutig in ein Regex-Muster. |
| `ArchivePathResolver` | Rein funktionale Logik, die aus dem Suchbegriff die Ordnerstruktur-Eingrenzung ableitet (keine Dateisystem-Zugriffe, daher ohne echte Verzeichnisse testbar). |
| `IFileSystemWalker` / `LocalFileSystemWalker` | Dünne Abstraktion über `System.IO.Directory`, austauschbar für Tests. |
| `FileArchiveIndexStore` | SQLite-Zugriff: Schema, Mark-and-Sweep-Aktualisierung, Selbstheilung. |
| `FileArchiveIndexService` | Orchestriert Voll- und Inkrementalscans, isoliert Fehler pro Element. |
| `FileArchiveSearchService` | Führt die eigentliche Suche aus (Index oder Live-Fallback), liefert Treffer als Stream. |

Auf der Desktop-Seite: `FileArchiveIndexHostedService` (Hintergrunddienst), `ShellLaunchService` (Datei/Ordner öffnen), `FileSearchViewModel`/`FileSearchView` (Oberfläche), `ArchiveSettingsPanelViewModel` (Einstellungen-Unterbereich).

## 3. Warum diese Suchstrategie schnell ist

Die Suchgeschwindigkeit beruht auf drei zusammenwirkenden Maßnahmen:

1. **Lokaler Index statt Netzlaufwerk-Zugriff pro Suche.** Sobald der Index einmal vollständig aufgebaut ist, läuft jede Suche ausschließlich gegen einen In-Memory-Snapshot der lokalen SQLite-Datenbank. Das Netzlaufwerk wird dabei überhaupt nicht kontaktiert – die Suchgeschwindigkeit hängt damit nicht mehr von Netzwerklatenz oder Serverlast ab. In den mitgelieferten Performance-Tests (siehe Abschnitt 13) dauert eine exakte Suche gegen einen warmen Index mit 8.000 indexierten Dateien wenige Millisekunden.
2. **Ordnerstruktur-Eingrenzung als Fallback vor der ersten vollständigen Indexierung.** Direkt nach dem Programmstart ist der Index noch nicht aufgebaut. Damit auch die allererste Suche nicht auf den (unter Umständen mehrere Minuten dauernden) ersten Vollscan warten muss, leitet `ArchivePathResolver` aus dem Suchbegriff die ersten drei Verzeichnisebenen ab (Standard 2/3/4 Zeichen) und greift nur auf den so ermittelten, meist sehr kleinen Teilbaum direkt zu. Ein Suchbegriff, der sich nicht eingrenzen lässt (z. B. `*123`), löst dabei bewusst **keinen** vollständigen Netzlaufwerk-Scan aus, sondern liefert (mit entsprechendem Hinweis in der Statuszeile) noch keine Treffer, bis der Index bereit ist – das Netzlaufwerk wird so nie durch eine einzelne Suche über Gebühr belastet.
3. **Geringe Hintergrundlast durch seltene, inkrementelle Aktualisierung.** Der Hintergrunddienst aktualisiert den Index standardmäßig nur einmal pro Stunde (konfigurierbar) und schreibt dabei unveränderte Dateien nicht neu (Erkennung über Größe/Änderungszeitpunkt), wodurch nur tatsächliche Änderungen Schreibaufwand verursachen. Das Netzlaufwerk wird dabei ausschließlich mit Verzeichnis-/Metadatenabfragen belastet, nie mit dem Lesen von Dateiinhalten.

Bewusst **nicht** verwendet wird ein `FileSystemWatcher` auf dem Netzlaufwerk: Änderungsbenachrichtigungen über SMB-Freigaben sind nachweislich unzuverlässig (stille Pufferüberläufe, inkonsistentes Verhalten je nach Serverversion) – ein periodischer Abgleich ist vorhersehbarer und ehrlicher als eine scheinbare Nahezu-Echtzeit-Aktualisierung, die gelegentlich lautlos Änderungen verpassen würde.

## 4. Installation

Die Dateisuche ist Teil der bestehenden ServiceApp2-Anwendung, kein separates Installationspaket. Voraussetzungen:

- .NET 8 Runtime (bzw. SDK für den Build) auf dem Windows-Client.
- Lese-/ggf. Schreibzugriff des angemeldeten Windows-Benutzers auf das konfigurierte Netzlaufwerk (die Anwendung selbst umgeht keinerlei Windows-Berechtigungen – siehe Abschnitt 12).
- Schreibzugriff auf `%AppData%\ServiceApp\` für Einstellungen, Logs und den lokalen Suchindex.

Build: `dotnet build ServiceApp.sln`. Start: `dotnet run --project ServiceApp.Desktop` (bzw. die erzeugte `ServiceApp.Desktop.exe`).

## 5. Konfiguration des Netzlaufwerks

Im Bereich **Einstellungen → Zeichnungsarchiv-Dateisuche**:

- **Archivpfad (Netzlaufwerk):** UNC-Pfad zum Archiv, z. B. `\\SERVER\Zeichnungsarchiv`. Nicht im Quellcode hinterlegt, sondern in `settings.json` gespeichert. Ein leerer oder gerade nicht erreichbarer Pfad verhindert nicht das Speichern der übrigen Einstellungen – das Netzlaufwerk darf beim Bearbeiten der Konfiguration offline sein.
- **Index-Speicherort:** Ordner für die lokale Indexdatenbank. Leer = Standardspeicherort `%AppData%\ServiceApp\index\filearchive-index.db`. Eine Änderung des Speicherorts wirkt nach einem Neustart der Anwendung.
- **Index automatisch aktualisieren:** an/aus.
- **Aktualisierungsintervall (Minuten):** Standard 60.
- **Maximale Anzahl angezeigter Treffer:** Standard 5000 – begrenzt sowohl die Anzahl tatsächlich gesuchter als auch angezeigter Treffer, schützt Oberfläche und Speicherverbrauch bei sehr unspezifischen Suchbegriffen.
- **Groß-/Kleinschreibung berücksichtigen:** Standard aus (Suche ist standardmäßig case-insensitiv).

## 6. Aufbau und Aktualisierung des Suchindex

**Erstmaliger Aufbau:** Beim ersten Start nach Konfiguration eines Archivpfads durchläuft der Hintergrunddienst automatisch einen vollständigen Scan: das gesamte Archiv wird rekursiv durchlaufen, jede Datei mit unterstützter Endung (siehe Abschnitt 8) wird mit Namen, Pfad, Größe und Änderungsdatum in die lokale Datenbank aufgenommen.

**Inkrementelle Aktualisierung:** Danach läuft periodisch (Standard: stündlich) ein erneuter vollständiger Verzeichnisdurchlauf, der aber nur tatsächliche Änderungen in die Datenbank schreibt:

- **Neue Dateien** werden erkannt (Pfad noch nicht im Index) und aufgenommen.
- **Gelöschte Dateien** werden erkannt: Jeder Scan markiert alle gesehenen Dateien mit einer fortlaufenden "Scan-Generation"; danach werden alle Zeilen im gescannten Bereich entfernt, deren Generation nicht der aktuellen entspricht (Mark-and-Sweep).
- **Umbenannte Dateien** werden dadurch automatisch korrekt als Löschung des alten Pfads plus Hinzufügen des neuen Pfads abgebildet – eine gesonderte "Rename-Erkennung" ist nicht nötig und würde nur unnötige Komplexität hinzufügen.
- **Unveränderte Dateien** (gleiche Größe und gleicher Änderungszeitpunkt wie beim letzten Scan) werden nicht neu geschrieben, nur als "weiterhin vorhanden" markiert – reduziert das Schreibvolumen bei den in der Praxis meist wenigen tatsächlichen Änderungen erheblich.
- **Fehler bei der Indexierung** (nicht lesbarer Unterordner, fehlende Berechtigung) brechen den Scan nicht ab, sondern werden protokolliert und als Anzahl übersprungener Elemente im Scan-Ergebnis gezählt.
- **Netzwerkunterbrechungen** während eines Scans führen zum Abbruch nur dieses einen Scan-Versuchs (Transaktion wird zurückgerollt, der bisherige Indexstand bleibt unverändert und gültig); der nächste planmäßige Versuch läuft normal weiter, bei nicht erreichbarem Archiv verkürzt sich der nächste Versuch automatisch auf 2 Minuten statt des vollen Intervalls.

**Manuelle Aktualisierung:** Über den Button **"Index jetzt aktualisieren"** in den Einstellungen, unabhängig vom automatischen Intervall.

## 7. Suchsyntax

| Eingabe | Bedeutung |
|---|---|
| `123456789` | **Exakter** Treffer (Groß-/Kleinschreibung standardmäßig ignoriert). Ohne Platzhalter wird der Dateiname ohne Endung exakt verglichen. |
| `*` | Beliebig lange (auch leere) Zeichenfolge. |
| `?` | Genau ein beliebiges Zeichen. |
| `123*` | Alles, was mit `123` beginnt. |
| `*789` | Alles, was mit `789` endet. |
| `*123*` | Alles, was `123` enthält (Teiltreffer erfordern explizite Platzhalter auf beiden Seiten). |
| `12345?789` | 9 Zeichen, wobei das 6. Zeichen beliebig ist (z. B. `123456789`, `12345A789`, aber **nicht** `12345789` oder `12345AB789`). |
| `12*45?789` | Kombination aus beiden Platzhaltern. |

Eine leere oder nur aus Leerzeichen bestehende Suche wird abgelehnt ("Bitte geben Sie einen Suchbegriff ein."). Sonderzeichen im Suchbegriff werden als literaler Text behandelt (kein Absturz, in aller Regel schlicht keine Treffer, da `*`/`?` in echten Windows-Dateinamen ohnehin nicht vorkommen können).

**Ordnerstruktur-Nutzung:** Die ersten drei Verzeichnisebenen des Archivs entsprechen (standardmäßig 2/3/4 Zeichen, konfigurierbar über `FileArchiveLevelLengths` in den Einstellungen) dem Anfang des Suchbegriffs, z. B. `123456789` → `\12\345\6789`. Diese Struktur wird sowohl für die Cold-Start-Live-Suche (Abschnitt 3) als auch – sofern der Suchbegriff dazu passt – zur weiteren Beschleunigung genutzt.

## 8. Unterstützte Dateitypen

Alle Dateitypen, TIF, PDF, JT, **DWG**, DOC, DOCX, DXF, XLS, XLSX.

> **Hinweis:** Die ursprüngliche Anforderung nannte den Dateityp "DWK". Da dies kein gängiges CAD-/Office-Format ist, wurde er nach Rücksprache als Tippfehler für **DWG** (AutoCAD-Zeichnung) interpretiert und entsprechend implementiert (`.dwg`).

Die Dateiendung wird unabhängig von Groß-/Kleinschreibung behandelt (`.pdf`, `.PDF`, `.Pdf` gelten als identisch). Nur Dateien mit einer der oben genannten Endungen werden überhaupt indexiert; alle anderen Dateien im Archiv werden beim Scan stillschweigend übergangen.

## 9. Start der Anwendung

`dotnet run --project ServiceApp.Desktop` bzw. die kompilierte `ServiceApp.Desktop.exe` starten. Über den Button **"Dateisuche"** in der oberen Navigationsleiste gelangt man zur Suchoberfläche. Beim ersten Start nach der Konfiguration eines Archivpfads beginnt der Hintergrunddienst automatisch mit dem ersten vollständigen Scan; Suchen sind schon währenddessen möglich (siehe Abschnitt 3, Cold-Start-Fallback).

## 10. Durchführung einer Indexaktualisierung

Siehe Abschnitt 6 – automatisch im konfigurierten Intervall, oder manuell über **Einstellungen → Zeichnungsarchiv-Dateisuche → "Index jetzt aktualisieren"**. Der Status (Anzahl indexierter Dateien, Zeitpunkt des letzten Vollscans, Anzahl übersprungener Elemente) wird direkt darunter angezeigt.

## 11. Fehlerbehebung

| Meldung / Situation | Ursache | Vorgehen |
|---|---|---|
| "Der konfigurierte Archivpfad wurde nicht gefunden" | Pfad falsch oder Freigabe existiert nicht (mehr). | Pfad in den Einstellungen prüfen. |
| "Das Netzlaufwerk ist derzeit nicht erreichbar" | Server/Netzwerk vorübergehend nicht erreichbar. | Netzwerkverbindung prüfen, erneut versuchen; der Hintergrunddienst versucht es automatisch in kürzerem Abstand erneut. |
| "Zugriff verweigert" | Der angemeldete Windows-Benutzer hat keine Berechtigung für den Pfad. | Berechtigungen mit dem zuständigen Administrator klären – die Anwendung kann und wird dies nicht umgehen. |
| Index scheint veraltet | Automatische Aktualisierung deaktiviert oder Intervall sehr lang. | "Index jetzt aktualisieren" manuell ausführen. |
| Suche liefert vor Programmstart kurz keine Treffer | Index noch nicht aufgebaut und Suchbegriff lässt sich nicht anhand der Ordnerstruktur eingrenzen. | Kurz warten, bis der erste Vollscan abgeschlossen ist (Statusanzeige in den Einstellungen). |
| "Datei konnte nicht geöffnet werden" | Datei zwischenzeitlich gelöscht/verschoben, oder keine passende Anwendung installiert. | Mit "Index jetzt aktualisieren" den Index auf den aktuellen Stand bringen. |
| Index wirkt beschädigt / Anwendung meldet Indexfehler beim Start | Unerwarteter Absturz während eines Schreibvorgangs o. Ä. | Kein Eingreifen nötig: der Index wird beim nächsten Start automatisch verworfen und neu aufgebaut. |

Alle Fehler werden zusätzlich in `%AppData%\ServiceApp\logs\serviceapp-*.log` protokolliert (Rolling-Log, 14 Tage Aufbewahrung).

## 12. Bekannte Einschränkungen

- **Kein Echtzeit-Änderungsabgleich.** Änderungen im Archiv werden erst mit der nächsten planmäßigen (oder manuell ausgelösten) Indexaktualisierung sichtbar, nicht sofort. Dies ist eine bewusste Entscheidung (siehe Abschnitt 3) und keine Fehlfunktion.
- **Kein Fallback bei fehlender Ebenen-Struktur pro Teilbereich.** Die Ebenen-Längen (Standard 2/3/4) gelten global für das gesamte Archiv. Sollte ein Teilbereich des realen Archivs strukturell abweichen, würde eine exakt eingrenzbare Live-Suche (vor Indexbereitschaft) dort fälschlich keine Treffer liefern; sobald der Index bereit ist, ist die Suche davon unabhängig korrekt, da der Index unabhängig von der Ordnerstruktur-Erwartung aufgebaut wird (Dateien außerhalb der 3-Ebenen-Struktur werden ganz normal mitindexiert, nur ohne Ebenen-Zuordnung).
- **Änderung des Index-Speicherorts wirkt erst nach Neustart.** Der Archivpfad selbst wird dagegen bei jedem automatischen Scan-Zyklus frisch aus den Einstellungen gelesen.
- **Entwicklungs-/Testumgebung.** Die automatisierten Tests dieser Anwendung laufen auch unter Linux (z. B. in CI-Umgebungen ohne Windows). Echte UNC-Pfad-Auflösung, Windows-ACL-basierte Zugriffsverweigerung und das Öffnen von Dateien über die Windows-Shell (`explorer.exe`, Standardanwendungen) lassen sich dort nicht nativ prüfen; diese Teile wurden durch Code-Review und die dafür vorgesehene, plattformunabhängig gehaltene Architektur (`IFileSystemWalker`-Abstraktion) so weit wie möglich abgesichert, ein abschließender Praxistest auf einem Windows-Client mit echtem Netzlaufwerkszugriff wird dennoch empfohlen.

## 13. Performance-Messwerte

Gemessen mit den mitgelieferten Performance-Tests (`ServiceApp.Tests/FileSearchTests/PerformanceTests.cs`) gegen einen synthetischen Verzeichnisbaum mit 8.000 Dateien, verteilt über 1.000 Blattordner (10×10×10 Ordner über die drei Ebenen):

| Messung | Ergebnis |
|---|---|
| Vollständiger Indexaufbau (8.000 Dateien) | ≈ 260 ms (≈ 31.000 Dateien/Sekunde) |
| Inkrementeller Scan (keine Änderungen) | ≈ 195 ms |
| Exakte Suche gegen warmen Index | ≈ 65 ms (erster Aufruf inkl. Laden des Snapshots aus SQLite) |
| Wildcard-Suche gegen warmen Index (1.000 Treffer) | ≈ 8 ms |
| Eingegrenzte Live-Suche vor Indexbereitschaft | ≈ 7 ms |

Diese Werte stammen aus der (schnellen, lokalen) Testumgebung und dienen als Plausibilitätsnachweis für die Architektur, nicht als verbindliche Zusagen für jede reale Netzlaufwerks-Umgebung – dort dominiert bei nicht indexierten Zugriffen zusätzlich die Netzwerklatenz zum Server, weshalb die Suche gegen den lokalen Index (statt direkt gegen das Netzlaufwerk) die entscheidende Maßnahme für konstant hohe Geschwindigkeit ist.
