using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;

namespace ImperaOps.Infrastructure.Templates;

/// <summary>
/// Generates realistic demo events spread over the past 14 months so
/// every dashboard date range (30D, 3M, 6M, 12M, YTD, All) has data.
/// Also seeds root causes and insight alerts.
/// </summary>
public static class DemoDataGenerator
{
    private static readonly Random Rng = new(42); // deterministic seed

    // ── Locations per industry ──────────────────────────────────────────

    private static readonly Dictionary<string, string[]> IndustryLocations = new()
    {
        ["tl-safety"] = new[]
        {
            "I-95 Northbound Mile 42", "Distribution Center A", "Truck Yard - Bay 12",
            "I-80 Westbound Rest Stop", "Warehouse Loading Dock 3", "Fuel Station - Route 66",
            "Cross-dock Facility B", "Highway 101 Interchange", "Terminal Gate 7", "Parking Lot C"
        },
        ["construction-safety"] = new[]
        {
            "Building A - Floor 3", "Excavation Site North", "Scaffolding Zone B2",
            "Main Tower - Level 12", "Parking Garage Foundation", "Electrical Room 101",
            "Crane Pad West", "Material Storage Yard", "Roof Section C", "Trench Line 4"
        },
        ["facilities-mgmt"] = new[]
        {
            "Lobby - Main Entrance", "Parking Garage Level B2", "Elevator Bank A",
            "Mechanical Room 3rd Floor", "Cafeteria Kitchen", "Server Room 201",
            "Stairwell B - 5th Floor", "Restroom - 2nd Floor East", "Loading Dock", "Roof Access"
        },
        ["manufacturing-safety"] = new[]
        {
            "Assembly Line 1", "Welding Station B", "Chemical Storage Room",
            "Forklift Aisle 3", "Press Room - Machine 7", "Paint Booth 2",
            "Quality Lab", "Shipping Bay 4", "Break Room", "Maintenance Shop"
        },
        ["saas-operations"] = new[]
        {
            "Platform Core - us-east-1", "API Gateway - production", "Auth Service - SSO",
            "Data Pipeline - sync-workers", "Dashboard - cdn-edge", "Integrations - webhook-relay",
            "Database - primary cluster", "Cache Layer - Redis", "CI/CD - deploy pipeline",
            "Monitoring - alerting stack"
        },
    };

    // ── Description templates per event type pattern ────────────────────

    private static string GenerateDescription(string eventTypeName, string location, string? templateId = null)
    {
        if (templateId == "saas-operations")
        {
            var templates = new[]
            {
                $"{eventTypeName} issue detected at {location}. On-call engineer paged. Monitoring dashboards show elevated error rates.",
                $"{eventTypeName} event at {location}. Automated alerting triggered. Team investigating root cause and assessing customer impact.",
                $"Degraded performance in {location} related to {eventTypeName.ToLower()}. P99 latency elevated. No data loss observed.",
                $"{eventTypeName} incident at {location}. Upstream dependency showing errors. Failover initiated while investigating root cause.",
                $"Alert fired for {eventTypeName.ToLower()} at {location}. Service partially degraded. Customer-facing impact being assessed.",
                $"{eventTypeName} at {location}. Deployment rollback under consideration. Error budget consumption rate elevated.",
                $"Automated monitoring detected {eventTypeName.ToLower()} at {location}. Runbook initiated. Status page updated.",
                $"{eventTypeName} event at {location}. Correlation with recent config change under investigation. Temporary mitigation applied.",
            };
            return templates[Rng.Next(templates.Length)];
        }

        var safetyTemplates = new[]
        {
            $"{eventTypeName} reported at {location}. Initial assessment underway. Area has been cordoned off pending investigation.",
            $"Incident involving {eventTypeName.ToLower()} occurred at {location}. Supervisor was notified immediately. No injuries reported at this time.",
            $"A {eventTypeName.ToLower()} event was observed at {location}. Witnesses have been identified and statements are being collected.",
            $"{eventTypeName} at {location}. Emergency response team dispatched. Situation is now under control and documentation in progress.",
            $"Report filed for {eventTypeName.ToLower()} at {location}. Contributing factors being assessed. Follow-up inspection scheduled.",
            $"A {eventTypeName.ToLower()} was documented at {location}. Corrective measures have been initiated. Management has been briefed.",
            $"Incident: {eventTypeName} at {location}. Root cause analysis in progress. Temporary safeguards have been implemented.",
            $"{eventTypeName} event at {location}. No immediate danger. Investigation team assigned to determine contributing factors.",
        };
        return safetyTemplates[Rng.Next(safetyTemplates.Length)];
    }

    /// <summary>
    /// Generates demo events, root causes, and insight alerts for a client.
    /// Call after ApplyTemplateToClientAsync so event types and statuses exist.
    /// </summary>
    public static async Task GenerateDemoDataAsync(
        ImperaOpsDbContext db,
        ICounterService counter,
        long clientId,
        string templateId,
        Dictionary<string, long> eventTypeKeyToId,
        Dictionary<string, long> statusKeyToId,
        long? ownerUserId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var locations = IndustryLocations.GetValueOrDefault(templateId) ?? IndustryLocations["facilities-mgmt"];

        // ── Root causes ────────────────────────────────────────────────
        var rootCauseNames = templateId == "saas-operations"
            ? new[]
            {
                "Cache / Memory Saturation", "Stale Configuration",
                "Import / Parser Error", "Certificate Expiration",
                "Deployment Regression", "Vendor / Third-Party Outage",
                "Infrastructure Capacity", "Data Integrity / Corruption",
                "Human Error", "Access / Permission Misconfiguration",
                "Regulatory / Compliance Gap", "Unknown / Under Investigation"
            }
            : new[]
            {
                "Human Error", "Equipment Failure", "Inadequate Training",
                "Procedural Gap", "Environmental Factor", "Material Defect",
                "Communication Breakdown", "Fatigue / Overwork"
            };
        var rootCauses = rootCauseNames.Select((name, i) => new RootCauseTaxonomyItem
        {
            ClientId  = clientId,
            Name      = name,
            SortOrder = i,
            CreatedAt = now,
        }).ToList();

        foreach (var rc in rootCauses) db.RootCauseTaxonomyItems.Add(rc);
        await db.SaveChangesAsync(ct);

        var rootCauseIds = rootCauses.Select(r => r.Id).ToList();

        // ── Resolve status keys for assignment ─────────────────────────
        // Find the first non-closed status (for open events) and closed statuses
        var openStatusIds   = statusKeyToId.Where(kv => !IsClosedKey(kv.Key)).Select(kv => kv.Value).ToList();
        var closedStatusIds = statusKeyToId.Where(kv => IsClosedKey(kv.Key)).Select(kv => kv.Value).ToList();

        if (openStatusIds.Count == 0) openStatusIds = statusKeyToId.Values.Take(1).ToList();
        if (closedStatusIds.Count == 0) closedStatusIds = statusKeyToId.Values.TakeLast(1).ToList();

        var allStatusIds = statusKeyToId.Values.ToList();
        var eventTypeKeys = eventTypeKeyToId.Keys.ToList();
        var eventTypeIds  = eventTypeKeyToId.Values.ToList();

        // ── Generate events spread over 14 months ──────────────────────
        // More recent months get more events for realistic trending
        var events = new List<Event>();
        var monthsBack = 14;

        for (int m = monthsBack; m >= 0; m--)
        {
            // Ramp up: older months have fewer events, recent months have more
            int baseCount = m switch
            {
                >= 12 => Rng.Next(3, 6),
                >= 6  => Rng.Next(5, 10),
                >= 3  => Rng.Next(8, 15),
                _     => Rng.Next(12, 20),
            };

            for (int i = 0; i < baseCount; i++)
            {
                var monthStart = now.AddMonths(-m);
                var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
                var day = Rng.Next(1, Math.Min(daysInMonth, 28) + 1);
                var hour = Rng.Next(6, 22);
                var minute = Rng.Next(0, 60);
                var occurredAt = new DateTimeOffset(monthStart.Year, monthStart.Month, day, hour, minute, 0, TimeSpan.Zero);

                // Don't create future events
                if (occurredAt > now) continue;

                var etIdx = Rng.Next(eventTypeKeys.Count);
                var etKey = eventTypeKeys[etIdx];
                var etId  = eventTypeIds[etIdx];

                var location = locations[Rng.Next(locations.Length)];
                var etName = etKey.Replace("_", " ");
                etName = char.ToUpper(etName[0]) + etName[1..];

                // Decide status: older events more likely to be closed
                long statusId;
                bool isClosed;
                if (m >= 2)
                {
                    // Mostly closed for older events
                    isClosed = Rng.NextDouble() < 0.85;
                    statusId = isClosed
                        ? closedStatusIds[Rng.Next(closedStatusIds.Count)]
                        : allStatusIds[Rng.Next(allStatusIds.Count)];
                }
                else if (m == 1)
                {
                    isClosed = Rng.NextDouble() < 0.5;
                    statusId = isClosed
                        ? closedStatusIds[Rng.Next(closedStatusIds.Count)]
                        : openStatusIds[Rng.Next(openStatusIds.Count)];
                }
                else
                {
                    // Current month: mostly open
                    isClosed = Rng.NextDouble() < 0.2;
                    statusId = isClosed
                        ? closedStatusIds[Rng.Next(closedStatusIds.Count)]
                        : openStatusIds[Rng.Next(openStatusIds.Count)];
                }

                // Assign root cause to ~60% of closed events and ~20% of open
                long? rootCauseId = null;
                string? correctiveAction = null;
                if (isClosed && Rng.NextDouble() < 0.6)
                {
                    rootCauseId = rootCauseIds[Rng.Next(rootCauseIds.Count)];
                    correctiveAction = GenerateCorrectiveAction(templateId);
                }
                else if (!isClosed && Rng.NextDouble() < 0.2)
                {
                    rootCauseId = rootCauseIds[Rng.Next(rootCauseIds.Count)];
                }

                var refNumber = await counter.AllocateAsync(clientId, "event", ct);

                var ev = new Event
                {
                    ClientId         = clientId,
                    PublicId         = $"EVT-{refNumber:D4}",
                    EventTypeId      = etId,
                    WorkflowStatusId = statusId,
                    Title            = GenerateTitle(etName, location, templateId),
                    OccurredAt       = occurredAt,
                    Location         = location,
                    Description      = GenerateDescription(etName, location, templateId),
                    OwnerUserId      = ownerUserId,
                    RootCauseId      = rootCauseId,
                    CorrectiveAction = correctiveAction,
                    ReferenceNumber  = refNumber,
                    CreatedAt        = occurredAt.AddMinutes(Rng.Next(5, 120)),
                    UpdatedAt        = isClosed
                        ? occurredAt.AddDays(Rng.Next(1, 14))
                        : occurredAt.AddMinutes(Rng.Next(30, 480)),
                };

                events.Add(ev);
                db.Events.Add(ev);
            }
        }

        await db.SaveChangesAsync(ct);

        // ── Insight alerts ─────────────────────────────────────────────
        var alertTypes = new (string Type, string Severity, string TitleTemplate, string BodyTemplate)[]
        {
            ("spike",    "critical", "Event spike detected in {location}",
             "A significant increase in events at {location} over the past 7 days. {count} events recorded, which is {pct}% above the 30-day average."),
            ("trend",    "warning",  "{type} events trending upward",
             "{type} events have increased {pct}% over the past 30 days compared to the prior period. Review procedures and training for this category."),
            ("pattern",  "info",     "Recurring pattern: {type} at {location}",
             "Multiple {type} events have been reported at {location} within the past 14 days. Consider a focused inspection or process review."),
            ("overdue",  "warning",  "{count} events past SLA closure deadline",
             "{count} open events have exceeded their SLA closure window. Prioritize resolution to maintain compliance targets."),
            ("positive", "info",     "Improvement: {type} events down {pct}%",
             "{type} events have decreased {pct}% compared to the prior 30-day period. Continue reinforcing current safety measures."),
        };

        // Generate 6-8 insight alerts spread over the last 30 days
        var alertCount = Rng.Next(6, 9);
        var topLocation = locations[0];
        var topType = eventTypeKeys[0].Replace("_", " ");
        topType = char.ToUpper(topType[0]) + topType[1..];

        for (int i = 0; i < alertCount; i++)
        {
            var template = alertTypes[i % alertTypes.Length];
            var daysAgo  = Rng.Next(0, 30);
            var genAt    = now.AddDays(-daysAgo);
            var loc      = locations[Rng.Next(locations.Length)];
            var typ      = eventTypeKeys[Rng.Next(eventTypeKeys.Count)].Replace("_", " ");
            typ = char.ToUpper(typ[0]) + typ[1..];
            var count = Rng.Next(3, 12);
            var pct   = Rng.Next(15, 85);

            var title = template.TitleTemplate
                .Replace("{location}", loc)
                .Replace("{type}", typ)
                .Replace("{count}", count.ToString())
                .Replace("{pct}", pct.ToString());

            var body = template.BodyTemplate
                .Replace("{location}", loc)
                .Replace("{type}", typ)
                .Replace("{count}", count.ToString())
                .Replace("{pct}", pct.ToString());

            // Pick some related event IDs from events generated this month
            var relatedIds = events
                .Where(e => e.Location == loc || Rng.NextDouble() < 0.1)
                .Take(5)
                .Select(e => e.Id.ToString())
                .ToList();

            db.InsightAlerts.Add(new InsightAlert
            {
                ClientId         = clientId,
                AlertType        = template.Type,
                Severity         = template.Severity,
                Title            = title,
                Body             = body,
                RelatedEventIds  = relatedIds.Count > 0 ? string.Join(",", relatedIds) : null,
                IsAcknowledged   = daysAgo > 14 && Rng.NextDouble() < 0.7,
                GeneratedAt      = genAt,
                ExpiresAt        = genAt.AddDays(30),
                CreatedAt        = genAt,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static bool IsClosedKey(string key)
        => key.Contains("resolved", StringComparison.OrdinalIgnoreCase)
        || key.Contains("closed", StringComparison.OrdinalIgnoreCase)
        || key.Contains("completed", StringComparison.OrdinalIgnoreCase);

    private static string GenerateTitle(string eventType, string location, string? templateId = null)
    {
        if (templateId == "saas-operations")
        {
            var templates = new[]
            {
                $"{eventType} issue in {location}",
                $"{location} - {eventType.ToLower()} alert",
                $"{eventType} degradation affecting {location}",
                $"{location}: {eventType.ToLower()} incident detected",
                $"{eventType} failure in {location} production environment",
            };
            return templates[Rng.Next(templates.Length)];
        }

        var safetyTemplates = new[]
        {
            $"{eventType} at {location}",
            $"{eventType} - {location}",
            $"{eventType} reported near {location}",
            $"{location}: {eventType} incident",
            $"{eventType} involving personnel at {location}",
        };
        return safetyTemplates[Rng.Next(safetyTemplates.Length)];
    }

    private static string GenerateCorrectiveAction(string? templateId = null)
    {
        if (templateId == "saas-operations")
        {
            var actions = new[]
            {
                "Increased worker pool capacity and added queue depth alerting. Runbook updated with escalation thresholds.",
                "Configuration drift corrected. Added automated config validation to deployment pipeline. Monitoring enhanced.",
                "Implemented canary deployment for this service. Added rollback automation triggered by error rate SLO breach.",
                "Certificate auto-renewal enabled. Added 30/14/7-day expiration alerts. Included in quarterly infrastructure audit.",
                "Added retry limits and circuit breaker pattern. Implemented fallback queue with dead-letter monitoring.",
                "Vendor failover path tested and documented. Added secondary provider integration. SLA review scheduled.",
                "Updated access control policies. Added permission change audit logging. Quarterly access review mandated.",
                "Data validation pipeline enhanced with schema checks. Added pre-production data quality gate for imports.",
            };
            return actions[Rng.Next(actions.Length)];
        }

        var safetyActions = new[]
        {
            "Retraining scheduled for all affected personnel. Updated SOP distributed to team leads. Follow-up audit scheduled in 30 days.",
            "Equipment has been repaired and recertified. Maintenance schedule updated to prevent recurrence. Supervisor sign-off required.",
            "Additional safety signage installed. Barrier added to prevent unauthorized access. Monthly inspection added to checklist.",
            "Process redesigned to eliminate hazard. New PPE requirements implemented. Training completed for all shifts.",
            "Root cause addressed through engineering controls. Work instructions updated. Toolbox talk conducted across all crews.",
            "Vendor notified and replacement parts ordered. Interim safeguard in place. Quality check added to receiving process.",
            "Communication protocol updated. Pre-shift briefing now includes this hazard. Buddy system implemented for high-risk tasks.",
            "Area redesigned to improve ergonomics. Rest break schedule adjusted. Occupational health follow-up scheduled.",
        };
        return safetyActions[Rng.Next(safetyActions.Length)];
    }
}
