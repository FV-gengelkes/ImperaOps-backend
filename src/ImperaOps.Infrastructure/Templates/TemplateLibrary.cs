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
    IReadOnlyList<TemplateCustomField>      CustomFields,
    IReadOnlyList<TemplateSlaRule>           SlaRules
);

public sealed record TemplateEventType(string Key, string Name, int SortOrder);

public sealed record TemplateWorkflowStatus(
    string Key, string Name, string? Color, bool IsClosed, int SortOrder);

public sealed record TemplateWorkflowTransition(
    string? FromStatusKey, string ToStatusKey, string? Label, bool IsDefault = false);

public sealed record TemplateCustomField(
    string Name, string DataType, bool IsRequired, int SortOrder, string? Options = null);

public sealed record TemplateSlaRule(
    string Name, string? EventTypeKey, int? InvestigationHours, int? ClosureHours);

// ── Library ───────────────────────────────────────────────────────────────────

public static class TemplateLibrary
{
    // ── Transportation & Logistics ──────────────────────────────────────────

    private static readonly EventTemplateDefinition TransportationSafety = new(
        Id:          "tl-safety",
        Name:        "Transportation & Logistics — Safety",
        Description: "DOT-compliant safety incident workflow with pre-built event types for accidents, " +
                     "near misses, cargo damage, HOS violations, and more. Includes severity tracking and " +
                     "root cause custom fields.",
        Industry:    "Transportation & Logistics",

        EventTypes: [
            new("cargo_damage",       "Cargo Damage / Loss", 1),
            new("driver_incident",    "Driver Incident",     2),
            new("hos_violation",      "HOS Violation",       3),
            new("near_miss",          "Near Miss",           4),
            new("route_deviation",    "Route Deviation",     5),
            new("security_incident",  "Security Incident",   6),
            new("vehicle_accident",   "Vehicle Accident",    7),
            new("vehicle_breakdown",  "Vehicle Breakdown",   8),
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
        ],

        SlaRules: []
    );

    // ── Construction Safety ─────────────────────────────────────────────────

    private static readonly EventTemplateDefinition ConstructionSafety = new(
        Id:          "construction-safety",
        Name:        "Construction — Safety",
        Description: "OSHA-aligned construction safety workflow covering falls, struck-by incidents, " +
                     "electrical hazards, caught-in/between events, and equipment failures. Includes " +
                     "severity classification, OSHA recordability tracking, and SLA deadlines.",
        Industry:    "Construction",

        EventTypes: [
            new("caught_in",         "Caught-In / Between",       1),
            new("chemical_exposure", "Chemical / Hazmat Exposure", 2),
            new("electrical",        "Electrical Hazard",          3),
            new("equipment_failure", "Equipment / Crane Failure",  4),
            new("excavation",        "Excavation / Trench Collapse", 5),
            new("fall",              "Fall / Fall from Height",    6),
            new("near_miss",         "Near Miss",                  7),
            new("property_damage",   "Property Damage",            8),
            new("scaffolding",       "Scaffolding Incident",       9),
            new("struck_by",         "Struck-By Incident",        10),
        ],

        WorkflowStatuses: [
            new("reported",            "Reported",             "#0EA5E9", false, 1),
            new("under_investigation", "Under Investigation",  "#F59E0B", false, 2),
            new("corrective_action",   "Corrective Action",    "#F97316", false, 3),
            new("escalated",           "Escalated to Safety",  "#DC2626", false, 4),
            new("resolved",            "Resolved",             "#16A34A", true,  5),
            new("closed",              "Closed",               "#64748B", true,  6),
        ],

        WorkflowTransitions: [
            new("reported",            "under_investigation", "Begin Investigation", IsDefault: true),
            new("reported",            "escalated",           "Escalate to Safety"),
            new("under_investigation", "corrective_action",   "Assign Corrective Action"),
            new("under_investigation", "escalated",           "Escalate to Safety"),
            new("under_investigation", "resolved",            "Resolve"),
            new("corrective_action",   "resolved",            "Mark Resolved"),
            new("corrective_action",   "escalated",           "Escalate to Safety"),
            new("escalated",           "under_investigation", "Return to Investigation"),
            new("escalated",           "resolved",            "Resolve"),
            new("resolved",            "closed",              "Close"),
            new("resolved",            "under_investigation", "Reopen"),
        ],

        CustomFields: [
            new("Job Site / Project",   "text",     false, 1),
            new("Contractor / Crew",    "text",     false, 2),
            new("Incident Severity",    "dropdown", false, 3, "[\"Minor\",\"Moderate\",\"Serious\",\"Critical / Fatal\"]"),
            new("OSHA Recordable",      "boolean",  false, 4),
            new("Body Part Affected",   "text",     false, 5),
            new("Equipment Involved",   "text",     false, 6),
            new("Weather Conditions",   "dropdown", false, 7, "[\"Clear\",\"Rain\",\"Wind\",\"Snow / Ice\",\"Extreme Heat\",\"Extreme Cold\"]"),
            new("Root Cause",           "dropdown", false, 8,
                "[\"Lack of Fall Protection\",\"Inadequate Training\",\"Equipment Failure\",\"Housekeeping\",\"PPE Non-Compliance\",\"Supervision Gap\",\"Other\"]"),
        ],

        SlaRules: [
            new("Default SLA",                   null,         4,   72),
            new("Fall Incidents",                 "fall",       2,   48),
            new("Electrical Hazards",             "electrical", 2,   48),
            new("Caught-In / Between Incidents",  "caught_in",  2,   48),
        ]
    );

    // ── Facilities Management ───────────────────────────────────────────────

    private static readonly EventTemplateDefinition FacilitiesManagement = new(
        Id:          "facilities-mgmt",
        Name:        "Facilities Management — Safety & Maintenance",
        Description: "Facilities management workflow for slip/trip/fall incidents, fire events, " +
                     "elevator/escalator malfunctions, HVAC failures, water damage, and security " +
                     "breaches. Includes location tracking and SLA response deadlines.",
        Industry:    "Facilities Management",

        EventTypes: [
            new("electrical",        "Electrical Issue",            1),
            new("elevator",          "Elevator / Escalator Issue",  2),
            new("fire",              "Fire / Smoke Event",          3),
            new("hazmat",            "Hazardous Material Spill",    4),
            new("hvac_failure",      "HVAC / Mechanical Failure",   5),
            new("security_breach",   "Security Breach",             6),
            new("slip_trip_fall",    "Slip / Trip / Fall",          7),
            new("structural",        "Structural Concern",          8),
            new("tenant_complaint",  "Tenant / Occupant Complaint", 9),
            new("water_damage",      "Water Damage / Leak",        10),
        ],

        WorkflowStatuses: [
            new("reported",       "Reported",           "#0EA5E9", false, 1),
            new("triaged",        "Triaged",            "#8B5CF6", false, 2),
            new("in_progress",    "In Progress",        "#F59E0B", false, 3),
            new("awaiting_parts", "Awaiting Parts / Vendor", "#F97316", false, 4),
            new("escalated",      "Escalated",          "#DC2626", false, 5),
            new("resolved",       "Resolved",           "#16A34A", true,  6),
            new("closed",         "Closed",             "#64748B", true,  7),
        ],

        WorkflowTransitions: [
            new("reported",       "triaged",        "Triage",             IsDefault: true),
            new("reported",       "escalated",      "Escalate"),
            new("triaged",        "in_progress",    "Start Work"),
            new("triaged",        "escalated",      "Escalate"),
            new("in_progress",    "awaiting_parts", "Awaiting Parts / Vendor"),
            new("in_progress",    "resolved",       "Resolve"),
            new("in_progress",    "escalated",      "Escalate"),
            new("awaiting_parts", "in_progress",    "Resume Work"),
            new("awaiting_parts", "escalated",      "Escalate"),
            new("escalated",      "in_progress",    "Return to Work"),
            new("escalated",      "resolved",       "Resolve"),
            new("resolved",       "closed",         "Close"),
            new("resolved",       "in_progress",    "Reopen"),
        ],

        CustomFields: [
            new("Building / Location",  "text",     false, 1),
            new("Floor / Zone",         "text",     false, 2),
            new("Severity",             "dropdown", false, 3, "[\"Low\",\"Medium\",\"High\",\"Critical\"]"),
            new("Injury Reported",      "boolean",  false, 4),
            new("Vendor Involved",      "text",     false, 5),
            new("Estimated Cost",       "text",     false, 6),
            new("Root Cause",           "dropdown", false, 7,
                "[\"Wear & Tear\",\"Lack of Maintenance\",\"Design Flaw\",\"Weather / Natural\",\"Vandalism\",\"Human Error\",\"Other\"]"),
        ],

        SlaRules: [
            new("Default SLA",       null,              4,   120),
            new("Fire Events",       "fire",            1,   24),
            new("Security Breaches", "security_breach",  1,   48),
            new("Elevator Issues",   "elevator",         2,   72),
        ]
    );

    // ── Manufacturing Safety ────────────────────────────────────────────────

    private static readonly EventTemplateDefinition ManufacturingSafety = new(
        Id:          "manufacturing-safety",
        Name:        "Manufacturing — Safety",
        Description: "Manufacturing safety workflow covering machine guarding incidents, " +
                     "lockout/tagout violations, ergonomic injuries, chemical exposures, and " +
                     "quality defects. Aligned with OSHA standards and includes shift tracking.",
        Industry:    "Manufacturing",

        EventTypes: [
            new("chemical_exposure",  "Chemical Exposure",            1),
            new("electrical",         "Electrical Incident",          2),
            new("environmental",      "Environmental Release",        3),
            new("ergonomic",          "Ergonomic / Repetitive Strain", 4),
            new("fire_explosion",     "Fire / Explosion",             5),
            new("forklift",           "Forklift / Vehicle Incident",  6),
            new("loto_violation",     "Lockout/Tagout Violation",     7),
            new("machine_injury",     "Machine / Equipment Injury",   8),
            new("quality_defect",     "Quality Defect / Near Miss",   9),
            new("slip_trip_fall",     "Slip / Trip / Fall",          10),
        ],

        WorkflowStatuses: [
            new("reported",            "Reported",            "#0EA5E9", false, 1),
            new("under_investigation", "Under Investigation", "#F59E0B", false, 2),
            new("root_cause_analysis", "Root Cause Analysis", "#8B5CF6", false, 3),
            new("corrective_action",   "Corrective Action",   "#F97316", false, 4),
            new("escalated",           "Escalated to EHS",    "#DC2626", false, 5),
            new("resolved",            "Resolved",            "#16A34A", true,  6),
            new("closed",              "Closed",              "#64748B", true,  7),
        ],

        WorkflowTransitions: [
            new("reported",            "under_investigation", "Begin Investigation", IsDefault: true),
            new("reported",            "escalated",           "Escalate to EHS"),
            new("under_investigation", "root_cause_analysis", "Start RCA"),
            new("under_investigation", "escalated",           "Escalate to EHS"),
            new("under_investigation", "resolved",            "Resolve"),
            new("root_cause_analysis", "corrective_action",   "Assign Corrective Action"),
            new("root_cause_analysis", "escalated",           "Escalate to EHS"),
            new("corrective_action",   "resolved",            "Mark Resolved"),
            new("corrective_action",   "escalated",           "Escalate to EHS"),
            new("escalated",           "under_investigation", "Return to Investigation"),
            new("escalated",           "resolved",            "Resolve"),
            new("resolved",            "closed",              "Close"),
            new("resolved",            "under_investigation", "Reopen"),
        ],

        CustomFields: [
            new("Production Line / Area", "text",     false, 1),
            new("Shift",                  "dropdown", false, 2, "[\"Day\",\"Swing\",\"Night\",\"Weekend\"]"),
            new("Machine / Equipment ID", "text",     false, 3),
            new("Incident Severity",      "dropdown", false, 4, "[\"First Aid\",\"Recordable\",\"Lost Time\",\"Fatality\"]"),
            new("OSHA Recordable",        "boolean",  false, 5),
            new("Body Part Affected",     "text",     false, 6),
            new("PPE Worn",               "boolean",  false, 7),
            new("Root Cause",             "dropdown", false, 8,
                "[\"Machine Guarding\",\"LOTO Failure\",\"Inadequate Training\",\"PPE Non-Compliance\",\"Housekeeping\",\"Fatigue\",\"Design Flaw\",\"Other\"]"),
        ],

        SlaRules: [
            new("Default SLA",            null,               4,   120),
            new("Machine Injuries",        "machine_injury",   2,   48),
            new("LOTO Violations",         "loto_violation",   2,   48),
            new("Fire / Explosion Events", "fire_explosion",   1,   24),
            new("Chemical Exposures",      "chemical_exposure", 2,   72),
        ]
    );

    public static readonly IReadOnlyDictionary<string, EventTemplateDefinition> All =
        new Dictionary<string, EventTemplateDefinition>
        {
            [TransportationSafety.Id]  = TransportationSafety,
            [ConstructionSafety.Id]    = ConstructionSafety,
            [FacilitiesManagement.Id]  = FacilitiesManagement,
            [ManufacturingSafety.Id]   = ManufacturingSafety,
        };
}
