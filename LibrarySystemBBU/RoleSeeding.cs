internal static class RoleSeeding
{
    internal static async Task SeedingRole(this WebApplication builder)
    {
        using var scope = builder.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<DatabaseSeed>();
        await roleService.AddRoles();
    }
}
