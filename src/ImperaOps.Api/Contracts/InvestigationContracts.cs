namespace ImperaOps.Api.Contracts;

public sealed record InvestigationDto(
    long Id,
    long ClientId,
    long EventId,
    string Status,
    string? Summary,
    string? RootCauseAnalysis,
    string? CorrectiveActions,
    long? LeadInvestigatorUserId,
    string? LeadInvestigatorName,
    string? StartedAt,
    string? CompletedAt,
    string CreatedAt,
    string UpdatedAt
);

public sealed record CreateInvestigationRequest(
    long? LeadInvestigatorUserId
);

public sealed record UpdateInvestigationRequest(
    string? Status,
    string? Summary,
    string? RootCauseAnalysis,
    string? CorrectiveActions,
    long? LeadInvestigatorUserId
);

public sealed record WitnessDto(
    long Id,
    long InvestigationId,
    string WitnessName,
    string? WitnessContact,
    string Statement,
    string? StatementDate,
    int SortOrder,
    string CreatedAt
);

public sealed record CreateWitnessRequest(
    string WitnessName,
    string? WitnessContact,
    string Statement,
    string? StatementDate
);

public sealed record UpdateWitnessRequest(
    string WitnessName,
    string? WitnessContact,
    string Statement,
    string? StatementDate
);

public sealed record EvidenceDto(
    long Id,
    long InvestigationId,
    string Title,
    string? Description,
    string EvidenceType,
    long? AttachmentId,
    string? CollectedAt,
    int SortOrder,
    string CreatedAt
);

public sealed record CreateEvidenceRequest(
    string Title,
    string? Description,
    string EvidenceType,
    long? AttachmentId,
    string? CollectedAt
);

public sealed record UpdateEvidenceRequest(
    string Title,
    string? Description,
    string EvidenceType,
    long? AttachmentId,
    string? CollectedAt
);
