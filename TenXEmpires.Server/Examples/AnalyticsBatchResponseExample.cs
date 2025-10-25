using Swashbuckle.AspNetCore.Filters;
using TenXEmpires.Server.Domain.DataContracts;

namespace TenXEmpires.Server.Examples;

public sealed class AnalyticsBatchResponseExample : IExamplesProvider<AnalyticsBatchResponse>
{
    public AnalyticsBatchResponse GetExamples() => new(2);
}
