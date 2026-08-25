using Argus.Domain.Models;

namespace Argus.Application.Interfaces;

public interface IEmailParser
{
    Task<ParsedEmail> ParseAsync(Stream emailStream, CancellationToken cancellationToken);
}