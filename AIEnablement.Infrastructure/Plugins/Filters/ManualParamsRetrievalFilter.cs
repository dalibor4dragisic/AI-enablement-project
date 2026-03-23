using Microsoft.SemanticKernel;

namespace AIEnablement.Infrastructure.Plugins.Filters;

public class ManualParamsRetrievalFilter : IAutoFunctionInvocationFilter
{
    private readonly int _topK;
    private readonly bool _includeNeighbors;
    private readonly double? _minRelevanceScore;

    public ManualParamsRetrievalFilter(int topK, bool includeNeighbors, double? minRelevanceScore)
    {
        _topK = topK;
        _includeNeighbors = includeNeighbors;
        _minRelevanceScore = minRelevanceScore;
    }

    public Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        // Dodaj parametar u argumente funkcije
        context.Arguments["topK"] = _topK;
        context.Arguments["includeNeighbors"] = _includeNeighbors;
        context.Arguments["minRelevanceScore"] = _minRelevanceScore;

        return next(context);
    }
}
