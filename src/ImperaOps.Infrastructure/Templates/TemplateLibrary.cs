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
    IReadOnlyList<TemplateSlaRule>           SlaRules,
    IReadOnlyList<TemplateWorkflowRule>     WorkflowRules
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

/// <summary>
/// A pre-built workflow rule to be created when a template is applied.
/// Conditions and actions use template keys (e.g. event type key, status key)
/// that get resolved to real IDs during application.
/// </summary>
public sealed record TemplateWorkflowRule(
    string Name,
    string? Description,
    string TriggerType,
    int SortOrder,
    bool StopOnMatch,
    IReadOnlyList<TemplateRuleCondition> Conditions,
    IReadOnlyList<TemplateRuleAction> Actions
);

public sealed record TemplateRuleCondition(string Field, string Operator, string? Value = null);

public sealed record TemplateRuleAction(
    string Type,
    string? StatusKey = null,
    string? TaskTitle = null,
    string? TaskDescription = null,
    int? TaskDueDaysFromNow = null,
    string[]? NotifyRoles = null,
    string? NotificationMessage = null,
    string? CommentBody = null
);

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

        SlaRules: [],

        WorkflowRules: [
            new("Auto-escalate vehicle accidents",
                "Escalate and notify managers when a vehicle accident is reported",
                "event.created", 1, false,
                [new("event_type_id", "equals", "vehicle_accident")],
                [
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A vehicle accident has been reported and requires immediate attention."),
                    new("create_task", TaskTitle: "Review accident report and contact driver",
                        TaskDueDaysFromNow: 1),
                ]),
            new("Notify on HOS violation",
                "Alert managers when an Hours of Service violation is logged",
                "event.created", 2, false,
                [new("event_type_id", "equals", "hos_violation")],
                [
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "An HOS violation has been reported — review for DOT compliance."),
                ]),
            new("Auto-comment on escalation",
                "Add a tracking comment when an event is escalated",
                "event.status_changed", 3, false,
                [new("workflow_status_id", "equals", "escalated")],
                [
                    new("add_comment", CommentBody: "This event has been escalated for priority review."),
                ]),
        ]
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
        ],

        WorkflowRules: [
            new("Escalate fall incidents",
                "Immediately escalate falls from height to safety team",
                "event.created", 1, false,
                [new("event_type_id", "equals", "fall")],
                [
                    new("change_status", StatusKey: "escalated"),
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A fall incident has been reported and auto-escalated to safety."),
                    new("create_task", TaskTitle: "Conduct fall protection audit at incident location",
                        TaskDueDaysFromNow: 2),
                ]),
            new("Track OSHA-recordable events",
                "Add review task when a caught-in or struck-by event is created",
                "event.created", 2, false,
                [new("event_type_id", "in", "caught_in,struck_by")],
                [
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A high-severity incident type has been reported — check OSHA recordability."),
                    new("create_task", TaskTitle: "Determine OSHA recordability and file if required",
                        TaskDueDaysFromNow: 3),
                ]),
            new("Auto-comment on escalation",
                "Add a tracking comment when an event is escalated to safety",
                "event.status_changed", 3, false,
                [new("workflow_status_id", "equals", "escalated")],
                [
                    new("add_comment", CommentBody: "This event has been escalated to the safety team for priority review."),
                ]),
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
        ],

        WorkflowRules: [
            new("Auto-escalate fire events",
                "Immediately escalate fire/smoke events and notify management",
                "event.created", 1, false,
                [new("event_type_id", "equals", "fire")],
                [
                    new("change_status", StatusKey: "escalated"),
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A fire/smoke event has been reported and auto-escalated."),
                ]),
            new("Security breach response",
                "Notify managers and create investigation task for security breaches",
                "event.created", 2, false,
                [new("event_type_id", "equals", "security_breach")],
                [
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A security breach has been reported — immediate review required."),
                    new("create_task", TaskTitle: "Review security footage and assess breach scope",
                        TaskDueDaysFromNow: 1),
                ]),
            new("Elevator issue notification",
                "Notify management when elevator/escalator issues are reported",
                "event.created", 3, false,
                [new("event_type_id", "equals", "elevator")],
                [
                    new("send_notification", NotifyRoles: ["Manager"],
                        NotificationMessage: "An elevator/escalator issue has been reported — arrange vendor inspection."),
                    new("create_task", TaskTitle: "Contact elevator maintenance vendor",
                        TaskDueDaysFromNow: 1),
                ]),
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
        ],

        WorkflowRules: [
            new("Auto-escalate LOTO violations",
                "Lockout/tagout violations are critical — escalate immediately",
                "event.created", 1, false,
                [new("event_type_id", "equals", "loto_violation")],
                [
                    new("change_status", StatusKey: "escalated"),
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A lockout/tagout violation has been reported and auto-escalated to EHS."),
                    new("create_task", TaskTitle: "Conduct LOTO compliance audit on affected equipment",
                        TaskDueDaysFromNow: 2),
                ]),
            new("Fire/explosion immediate response",
                "Auto-escalate and notify for fire or explosion events",
                "event.created", 2, false,
                [new("event_type_id", "equals", "fire_explosion")],
                [
                    new("change_status", StatusKey: "escalated"),
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A fire/explosion event has been reported — emergency response may be required."),
                ]),
            new("Machine injury review task",
                "Create an investigation task for machine/equipment injuries",
                "event.created", 3, false,
                [new("event_type_id", "equals", "machine_injury")],
                [
                    new("send_notification", NotifyRoles: ["Manager"],
                        NotificationMessage: "A machine/equipment injury has been reported."),
                    new("create_task", TaskTitle: "Inspect machine guarding and safety interlocks",
                        TaskDueDaysFromNow: 2),
                ]),
        ]
    );

    // ── SaaS / Software Operations ────────────────────────────────────────

    private static readonly EventTemplateDefinition SaasOperations = new(
        Id:          "saas-operations",
        Name:        "SaaS / Software — Operations",
        Description: "Operational incident management for SaaS and software companies. Covers " +
                     "infrastructure outages, deployment failures, security incidents, customer " +
                     "escalations, vendor issues, compliance work, and data integrity events. " +
                     "Includes severity tracking, product/team assignment, and SLA deadlines.",
        Industry:    "Software / SaaS",

        EventTypes: [
            new("infrastructure", "Infrastructure",  1),
            new("deployment",     "Deployment",      2),
            new("security",       "Security",        3),
            new("compliance",     "Compliance",      4),
            new("customer",       "Customer",        5),
            new("data",           "Data",            6),
            new("vendor",         "Vendor",          7),
            new("operational",    "Operational",     8),
        ],

        WorkflowStatuses: [
            new("open",           "Open",           "#3B82F6", false, 1),
            new("acknowledged",   "Acknowledged",   "#F59E0B", false, 2),
            new("investigating",  "Investigating",  "#8B5CF6", false, 3),
            new("mitigated",      "Mitigated",      "#14B8A6", false, 4),
            new("resolved",       "Resolved",       "#16A34A", true,  5),
            new("closed",         "Closed",         "#64748B", true,  6),
        ],

        WorkflowTransitions: [
            new(null,             "open",           null,                 IsDefault: true),
            new("open",           "acknowledged",   "Acknowledge"),
            new("acknowledged",   "investigating",  "Begin Investigation"),
            new("investigating",  "mitigated",      "Mitigate"),
            new("mitigated",      "resolved",       "Resolve"),
            new("resolved",       "closed",         "Close"),
            // Skip-ahead shortcuts
            new("open",           "investigating",  "Investigate"),
            new("investigating",  "resolved",       "Resolve"),
            new("acknowledged",   "resolved",       "Resolve"),
            new("open",           "closed",         "Close"),
        ],

        CustomFields: [
            new("Severity",        "dropdown", true,  1, "[\"Critical\",\"High\",\"Medium\",\"Low\"]"),
            new("Product",         "dropdown", false, 2, "[\"Platform\",\"API\",\"Dashboard\",\"Integrations\",\"Data Pipeline\",\"Auth / SSO\"]"),
            new("Team",            "dropdown", false, 3, "[\"Engineering\",\"Platform / DevOps\",\"Support\",\"Customer Success\",\"Compliance\",\"Security\",\"Product\",\"Leadership\"]"),
            new("Customer Impact", "dropdown", false, 4, "[\"None\",\"Low - Few Users\",\"Medium - Many Users\",\"High - All Users\",\"Strategic Account\"]"),
            new("External Ticket", "url",      false, 5),
        ],

        SlaRules: [
            new("Default SLA",              null,               4,  48),
            new("Critical Infrastructure",  "infrastructure",   1,  4),
            new("Security Incidents",       "security",         1,  8),
            new("Customer Escalations",     "customer",         2,  24),
            new("Data Incidents",           "data",             2,  12),
            new("Compliance Items",         "compliance",       4,  72),
        ],

        WorkflowRules: [
            new("Auto-acknowledge infrastructure incidents",
                "Move infrastructure events to Acknowledged status immediately",
                "event.created", 1, false,
                [new("event_type_id", "equals", "infrastructure")],
                [
                    new("change_status", StatusKey: "acknowledged"),
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "An infrastructure incident has been reported and auto-acknowledged."),
                ]),
            new("Security incident response",
                "Escalate security incidents and create triage task",
                "event.created", 2, false,
                [new("event_type_id", "equals", "security")],
                [
                    new("send_notification", NotifyRoles: ["Manager", "Admin"],
                        NotificationMessage: "A security incident has been reported — initiate incident response."),
                    new("create_task", TaskTitle: "Assess security impact and determine if customer data is affected",
                        TaskDueDaysFromNow: 1),
                ]),
            new("Customer escalation tracking",
                "Create follow-up task for customer-reported incidents",
                "event.created", 3, false,
                [new("event_type_id", "equals", "customer")],
                [
                    new("send_notification", NotifyRoles: ["Manager"],
                        NotificationMessage: "A customer escalation has been created."),
                    new("create_task", TaskTitle: "Send initial customer communication within 1 hour",
                        TaskDueDaysFromNow: 1),
                ]),
        ]
    );

    public static readonly IReadOnlyDictionary<string, EventTemplateDefinition> All =
        new Dictionary<string, EventTemplateDefinition>
        {
            [TransportationSafety.Id]  = TransportationSafety,
            [ConstructionSafety.Id]    = ConstructionSafety,
            [FacilitiesManagement.Id]  = FacilitiesManagement,
            [ManufacturingSafety.Id]   = ManufacturingSafety,
            [SaasOperations.Id]        = SaasOperations,
        };
}
