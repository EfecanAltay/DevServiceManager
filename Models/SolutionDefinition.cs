using System.Collections.Generic;

namespace DevManager.Models;

/// <summary>
/// Bir grup servisten oluşan çözüm tanımı.
/// </summary>
public class SolutionDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<ServiceDefinition> Services { get; set; } = new();
}
