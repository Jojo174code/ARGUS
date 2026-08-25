namespace Argus.Domain.Models;

public sealed record AttackTechniqueFinding(
    string TechniqueId,
    string Name,
    string Basis);