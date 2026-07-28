using ServiceApp.Core.Models;
using ServiceApp.Core.TicketParser;

namespace ServiceApp.Core.Repository;

/// <summary>
/// Standard-Implementierung von <see cref="ITicketRepository"/>: delegiert das
/// eigentliche Einlesen an <see cref="IServiceTicketParser"/>. Die Trennung von
/// Repository und Parser hält den Import-Mechanismus (Excel/ClosedXML) austauschbar,
/// ohne dass Aufrufer des Repositories davon wissen müssen.
/// </summary>
public sealed class ExcelTicketRepository : ITicketRepository
{
    private readonly IServiceTicketParser _parser;

    public ExcelTicketRepository(IServiceTicketParser parser)
    {
        _parser = parser;
    }

    /// <inheritdoc />
    public IReadOnlyList<ServiceTicket> GetTickets(string filePath) => _parser.Parse(filePath);
}
