namespace TruthInTheFlip.Format;

public abstract class MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Get an absolute value for an input.")]
    public double abs(double input)
    {
        return Math.Abs(input);
    }
}

public abstract class MetricFunctionsAggregate : MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the average for an input.")]
    public double mean(List<double> input)
    {
        if (input.Count == 0)
            return double.NaN;
        
        double sum = 0;
        foreach (double value in input)
        {
            sum += value;
        }
        return sum / input.Count;
    }
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the sum for an input.")]
    public double sum(List<double> input)
    {
        double sum = 0;
        foreach (double value in input)
        {
            sum += value;
        }
        return sum;
    }

}