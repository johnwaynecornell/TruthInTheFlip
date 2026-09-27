using TruthInTheFlip.Format;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// Root stats item yielded by <see cref="WrapProcess"/> representing a wrapped population of child items.
/// Exposes aggregate metric functions (e.g. mean, max, sum) to outer metric binders and formatters.
/// </summary>
public sealed class WrapStats : MetricFunctionsAggregate
{
}
