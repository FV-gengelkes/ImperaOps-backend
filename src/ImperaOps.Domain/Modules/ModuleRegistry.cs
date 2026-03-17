namespace ImperaOps.Domain.Modules;

public sealed record ModuleDefinition(
    string Id,
    string Name,
    string Description,
    string Icon,
    string Category
);

public static class ModuleRegistry
{
    public static readonly IReadOnlyDictionary<string, ModuleDefinition> All =
        new Dictionary<string, ModuleDefinition>
        {
            ["ag_field_mapping"] = new(
                "ag_field_mapping",
                "Precision Ag Field Mapping",
                "Track GPS coordinates, field boundaries, and precision agriculture data for drone spraying and crop management.",
                "MapPin",
                "Industry"
            ),
            ["crm"] = new(
                "crm",
                "Customer Relationship Management",
                "Manage client contacts, communication history, and sales pipeline alongside your operations.",
                "Users",
                "Business"
            ),
            ["payment_processing"] = new(
                "payment_processing",
                "Payment Processing",
                "Invoice clients, track payments, and manage billing directly within your operational workflow.",
                "CreditCard",
                "Business"
            ),
        };

    public static bool Exists(string moduleId) => All.ContainsKey(moduleId);
}
