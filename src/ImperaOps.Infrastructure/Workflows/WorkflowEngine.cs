using System.Text.Json;
using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowRuleRepository _rules;
    private readonly IEventMutator _mutator;
    private readonly IWorkflowNotifier _notifier;
    private readonly IWorkflowActionService _actions;
    private readonly ILogger<WorkflowEngine> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public WorkflowEngine(
        IWorkflowRuleRepository rules,
        IEventMutator mutator,
        IWorkflowNotifier notifier,
        IWorkflowActionService actions,
        ILogger<WorkflowEngine> logger)
    {
        _rules    = rules;
        _mutator  = mutator;
        _notifier = notifier;
        _actions  = actions;
        _logger   = logger;
    }

    public async Task EvaluateAsync(string triggerType, Event ev, Event? previousSnapshot, CancellationToken ct = default)
    {
        try
        {
            var rules = await _rules.GetActiveRulesAsync(ev.ClientId, triggerType, ct);
            if (rules.Count == 0) return;

            foreach (var rule in rules)
            {
                var conditions = DeserializeConditions(rule.ConditionsJson);
                var actions = DeserializeActions(rule.ActionsJson);

                if (conditions.Count == 0 && actions.Count == 0) continue;

                if (!AllConditionsMatch(conditions, ev, previousSnapshot))
                    continue;

                var actionsExecuted = 0;
                string? error = null;

                try
                {
                    actionsExecuted = await ExecuteActionsAsync(actions, ev, rule, ct);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    _logger.LogWarning(ex, "Workflow rule {RuleId} action execution failed for event {EventId}", rule.Id, ev.Id);
                }

                await _rules.SaveExecutionAsync(new WorkflowRuleExecution
                {
                    WorkflowRuleId = rule.Id,
                    ClientId = ev.ClientId,
                    EventId = ev.Id,
                    TriggerType = triggerType,
                    ActionsExecuted = actionsExecuted,
                    Success = error == null,
                    ErrorMessage = error,
                    ExecutedAt = DateTimeOffset.UtcNow,
                }, ct);

                if (rule.StopOnMatch) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow engine evaluation failed for trigger {Trigger} event {EventId}", triggerType, ev.Id);
        }
    }

    // ── Condition Evaluation ──────────────────────────────────────────────

    private static bool AllConditionsMatch(List<WorkflowCondition> conditions, Event ev, Event? prev)
    {
        foreach (var c in conditions)
        {
            if (!EvaluateCondition(c, ev, prev))
                return false;
        }
        return true;
    }

    private static bool EvaluateCondition(WorkflowCondition c, Event ev, Event? prev)
    {
        var fieldValue = GetFieldValue(c.Field, ev);
        var op = c.Operator.ToLowerInvariant();

        return op switch
        {
            "equals"       => string.Equals(fieldValue, c.Value, StringComparison.OrdinalIgnoreCase),
            "not_equals"   => !string.Equals(fieldValue, c.Value, StringComparison.OrdinalIgnoreCase),
            "contains"     => fieldValue != null && c.Value != null && fieldValue.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            "not_contains" => fieldValue == null || c.Value == null || !fieldValue.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            "starts_with"  => fieldValue != null && c.Value != null && fieldValue.StartsWith(c.Value, StringComparison.OrdinalIgnoreCase),
            "is_null"      => string.IsNullOrEmpty(fieldValue) || fieldValue == "0",
            "is_not_null"  => !string.IsNullOrEmpty(fieldValue) && fieldValue != "0",
            "in"           => c.Value != null && c.Value.Split(',').Select(v => v.Trim()).Contains(fieldValue, StringComparer.OrdinalIgnoreCase),
            "changed"      => prev != null && GetFieldValue(c.Field, prev) != fieldValue,
            "changed_to"   => prev != null && GetFieldValue(c.Field, prev) != fieldValue && string.Equals(fieldValue, c.Value, StringComparison.OrdinalIgnoreCase),
            _              => false,
        };
    }

    private static string? GetFieldValue(string field, Event ev) => field.ToLowerInvariant() switch
    {
        "event_type_id"      => ev.EventTypeId.ToString(),
        "workflow_status_id" => ev.WorkflowStatusId.ToString(),
        "location"           => ev.Location,
        "title"              => ev.Title,
        "description"        => ev.Description,
        "owner_user_id"      => ev.OwnerUserId?.ToString(),
        "root_cause_id"      => ev.RootCauseId?.ToString(),
        _                    => null,
    };

    // ── Action Execution ──────────────────────────────────────────────────

    private async Task<int> ExecuteActionsAsync(List<WorkflowAction> actions, Event ev, WorkflowRule rule, CancellationToken ct)
    {
        var count = 0;

        foreach (var action in actions)
        {
            var c = action.Config;

            switch (action.Type.ToLowerInvariant())
            {
                case "assign_owner" when c.UserId.HasValue:
                    await _mutator.AssignOwnerAsync(ev.Id, ev.ClientId, c.UserId.Value, rule.Name, ct);
                    await _notifier.NotifyEventAssignedAsync(c.UserId.Value, ev.ClientId, ev.PublicId, ev.Title, ct);
                    count++;
                    break;

                case "change_status" when c.WorkflowStatusId.HasValue:
                    await _mutator.ChangeStatusAsync(ev.Id, ev.ClientId, c.WorkflowStatusId.Value, rule.Name, ct);
                    count++;
                    break;

                case "create_task" when !string.IsNullOrWhiteSpace(c.TaskTitle):
                    await _actions.CreateTaskAsync(ev.ClientId, ev.Id, c.TaskTitle, c.TaskDescription,
                        c.TaskAssignedToUserId, c.TaskDueDaysFromNow, rule.Name, ct);
                    count++;
                    break;

                case "send_notification":
                    var message = c.NotificationMessage ?? $"Workflow rule \"{rule.Name}\" triggered for event {ev.PublicId}.";
                    await _notifier.NotifyUsersAsync(ev.ClientId, ev.PublicId, rule.Name, message,
                        c.NotifyUserIds, c.NotifyRoles, ct);
                    count++;
                    break;

                case "add_comment" when !string.IsNullOrWhiteSpace(c.CommentBody):
                    await _actions.AddCommentAsync(ev.Id, ev.ClientId, c.CommentBody, ct);
                    count++;
                    break;

                case "set_root_cause" when c.RootCauseId.HasValue:
                    await _mutator.SetRootCauseAsync(ev.Id, ev.ClientId, c.RootCauseId.Value, rule.Name, ct);
                    count++;
                    break;

                case "round_robin_assign" when c.RoundRobinUserIds is { Length: > 0 }:
                    var nextUserId = await _actions.GetNextRoundRobinUserAsync(rule.Id, c.RoundRobinUserIds, ct);
                    await _mutator.AssignOwnerAsync(ev.Id, ev.ClientId, nextUserId, rule.Name, ct);
                    await _notifier.NotifyEventAssignedAsync(nextUserId, ev.ClientId, ev.PublicId, ev.Title, ct);
                    count++;
                    break;

                default:
                    _logger.LogWarning("Unknown or misconfigured workflow action type: {Type}", action.Type);
                    break;
            }
        }

        return count;
    }

    // ── Serialization ─────────────────────────────────────────────────────

    private static List<WorkflowCondition> DeserializeConditions(string json)
    {
        try { return JsonSerializer.Deserialize<List<WorkflowCondition>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }

    private static List<WorkflowAction> DeserializeActions(string json)
    {
        try { return JsonSerializer.Deserialize<List<WorkflowAction>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }
}
