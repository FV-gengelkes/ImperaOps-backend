namespace ImperaOps.Infrastructure.Templates;

// ── Value types ───────────────────────────────────────────────────────────────

public sealed record EventTemplateDefinition(
    string Id,
    string Name,
    string Description,
    string Industry,
    IReadOnlyList<TemplateEventType>        EventTypes,
    IReadOnlyList<TemplateWorkflowStatus>   WorkflowStatuses,
    IReadOnlyList<TemplateWorkflowTransition> WorkflowTransitions,
    IReadOnlyList<TemplateCustomField>      CustomFields
);

public sealed record TemplateEventType(string Key, string Name, int SortOrder);

public sealed record TemplateWorkflowStatus(
    string Key, string Name, string? Color, bool IsClosed, int SortOrder);

public sealed record TemplateWorkflowTransition(
    string? FromStatusKey, string ToStatusKey, string? Label, bool IsDefault = false);

public sealed record TemplateCustomField(
    string Name, string DataType, bool IsRequired, int SortOrder, string? Options = null);

// ── Library ───────────────────────────────────────────────────────────────────

public static class TemplateLibrary
{
    private static readonly EventTemplateDefinition TransportationSafety = new(
        Id:          "tl-safety",
        Name:        "Transportation & Logistics — Safety",
        Description: "DOT-compliant safety incident workflow with pre-built event types for accidents, " +
                     "near misses, cargo damage, HOS violations, and more. Includes severity tracking and " +
                     "root cause custom fields.",
        Industry:    "Transportation & Logistics",

        EventTypes: [
            new("vehicle_accident",   "Vehicle Accident",    1),
            new("near_miss",          "Near Miss",           2),
            new("cargo_damage",       "Cargo Damage / Loss", 3),
            new("hos_violation",      "HOS Violation",       4),
            new("vehicle_breakdown",  "Vehicle Breakdown",   5),
            new("driver_incident",    "Driver Incident",     6),
            new("route_deviation",    "Route Deviation",     7),
            new("security_incident",  "Security Incident",   8),
        ],

        WorkflowStatuses: [
            new("new",                 "New",                  "#0EA5E9", false, 1),
            new("under_investigation", "Under Investigation",   "#F59E0B", false, 2),
            new("pending_action",      "Pending Action",        "#F97316", false, 3),
            new("escalated",           "Escalated",             "#DC2626", false, 4),
            new("resolved",            "Resolved",              "#16A34A", true,  5),
            new("closed",              "Closed",                "#64748B", true,  6),
        ],

        WorkflowTransitions: [
            new("new",                 "under_investigation", "Begin Review",    IsDefault: true),
            new("new",                 "escalated",           "Escalate"),
            new("under_investigation", "pending_action",      "Action Required"),
            new("under_investigation", "escalated",           "Escalate"),
            new("under_investigation", "resolved",            "Resolve"),
            new("pending_action",      "resolved",            "Mark Resolved"),
            new("pending_action",      "escalated",           "Escalate"),
            new("escalated",           "under_investigation", "Return to Review"),
            new("escalated",           "resolved",            "Resolve"),
            new("resolved",            "closed",              "Close"),
            new("resolved",            "under_investigation", "Reopen"),
        ],

        CustomFields: [
            new("Vehicle ID",        "text",     false, 1),
            new("Driver Name",       "text",     false, 2),
            new("Location / Route",  "text",     false, 3),
            new("Incident Severity", "dropdown", false, 4, "[\"Low\",\"Medium\",\"High\",\"Critical\"]"),
            new("DOT Reportable",    "boolean",  false, 5),
            new("Fleet Unit #",      "text",     false, 6),
            new("Load # / BOL",      "text",     false, 7),
            new("Root Cause",        "dropdown", false, 8,
                "[\"Driver Error\",\"Mechanical Failure\",\"Weather\",\"Road Conditions\",\"External Factor\",\"Other\"]"),
        ]
    );

    public static readonly IReadOnlyDictionary<string, EventTemplateDefinition> All =
        new Dictionary<string, EventTemplateDefinition>
        {
            [TransportationSafety.Id] = TransportationSafety,
        };
}
