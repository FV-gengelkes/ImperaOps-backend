using ImperaOps.Application.Abstractions;
using ImperaOps.Domain.Entities;
using ImperaOps.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ImperaOps.Infrastructure.Workflows;

public sealed class WorkflowRuleRepository : IWorkflowRuleRepository
{
    private readonly ImperaOpsDbContext _db;

    public WorkflowRuleRepository(ImperaOpsDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkflowRule>> GetActiveRulesAsync(long clientId, string triggerType, CancellationToken ct)
    {
        return await _db.WorkflowRules
            .AsNoTracking()
            .Where(r => r.ClientId == clientId && r.TriggerType == triggerType && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task SaveExecutionAsync(WorkflowRuleExecution execution, CancellationToken ct)
    {
        _db.WorkflowRuleExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);
    }
}
