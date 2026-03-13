namespace ImperaOps.Infrastructure.Workflows;

/// <summary>
/// A single condition within a workflow rule.
/// Serialized as JSON array in WorkflowRule.ConditionsJson.
/// </summary>
public sealed class WorkflowCondition
{
    /// <summary>
    /// Field to evaluate: event_type_id, workflow_status_id, location, title,
    /// description, owner_user_id, root_cause_id
    /// </summary>
    public string Field { get; set; } = "";

    /// <summary>
    /// Operator: equals, not_equals, contains, not_contains, starts_with,
    /// is_null, is_not_null, in, greater_than, less_than
    /// </summary>
    public string Operator { get; set; } = "";

    /// <summary>
    /// Value to compare against. For "in" operator, use comma-separated IDs.
    /// For is_null/is_not_null, this is ignored.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// A single action within a workflow rule.
/// Serialized as JSON array in WorkflowRule.ActionsJson.
/// </summary>
public sealed class WorkflowAction
{
    /// <summary>
    /// Action type: assign_owner, change_status, create_task, send_notification,
    /// add_comment, set_root_cause
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>JSON config specific to the action type.</summary>
    public WorkflowActionConfig Config { get; set; } = new();
}

/// <summary>
/// Union config for all action types. Only relevant fields are populated per type.
/// </summary>
public sealed class WorkflowActionConfig
{
    // assign_owner
    public long? UserId { get; set; }

    // change_status
    public long? WorkflowStatusId { get; set; }

    // create_task
    public string? TaskTitle { get; set; }
    public string? TaskDescription { get; set; }
    public long? TaskAssignedToUserId { get; set; }
    public int? TaskDueDaysFromNow { get; set; }

    // send_notification
    public long[]? NotifyUserIds { get; set; }
    public string[]? NotifyRoles { get; set; }
    public string? NotificationMessage { get; set; }

    // add_comment
    public string? CommentBody { get; set; }

    // set_root_cause
    public long? RootCauseId { get; set; }

    // round_robin_assign
    public long[]? RoundRobinUserIds { get; set; }
}
