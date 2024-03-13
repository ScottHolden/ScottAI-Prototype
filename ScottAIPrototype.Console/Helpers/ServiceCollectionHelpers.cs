using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionHelpers
{
    public static IServiceCollection BindConfiguration<T>(this IServiceCollection services, string sectionName) where T : class
        => services.AddTransient((context)
            => context.GetRequiredService<IConfiguration>().GetSection(sectionName).Get<T>()
                ?? throw new Exception($"Unable to bind section {sectionName} to {typeof(T)}"));
    public static IServiceCollection BindConfiguration<T>(this IServiceCollection services) where T : class
        => services.AddTransient((context)
            => context.GetRequiredService<IConfiguration>().Get<T>()
                ?? throw new Exception($"Unable to bind to {typeof(T)}"));
}
