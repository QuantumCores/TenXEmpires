namespace TenXEmpires.Server.Domain.Services;

/// <summary>
/// Service for generating AI opponent names.
/// </summary>
public interface IAiNameGenerator
{
    /// <summary>
    /// Generates a random name for an AI opponent using historical or fantasy leaders.
    /// </summary>
    /// <param name="seed">Optional seed for deterministic name generation.</param>
    /// <returns>A name for the AI opponent.</returns>
    string GenerateName(long? seed = null);
}

