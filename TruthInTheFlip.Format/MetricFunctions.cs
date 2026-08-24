namespace TruthInTheFlip.Format;

public abstract class MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Get the absolute value of an input.")]
    public double abs(double value)
    {
        return Math.Abs(value);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Negate an input value.")]
    public double negate(double value)
    {
        return -value;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Square an input value.")]
    public double square(double value)
    {
        return value * value;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Get the square root of an input.")]
    public double sqrt(double value)
    {
        return Math.Sqrt(value);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Get the natural logarithm of an input.")]
    public double ln(double value)
    {
        return Math.Log(value);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Raise a value to a power.")]
    public double pow(double value, double exponent)
    {
        return Math.Pow(value, exponent);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Add an offset to an input value.")]
    public double offset(double value, double amount)
    {
        return value + amount;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Measure an input relative to the 50 percent baseline.")]
    public double offset50(double value)
    {
        return value - 50.0;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Multiply an input by a scale factor.")]
    public double scale(double value, double factor)
    {
        return value * factor;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Divide one input by another.")]
    public double ratio(double numerator, double denominator)
    {
        return numerator / denominator;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Limit an input to the inclusive minimum and maximum.")]
    public double clamp(double value, double min, double max)
    {
        return Math.Clamp(value, min, max);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Linearly interpolate between two values.")]
    public double lerp(double a, double b, double amount)
    {
        return a + ((b - a) * amount);
    }
}


public abstract class MetricFunctionsAggregate : MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the number of values in an input population.")]
    public double count(List<double> values)
    {
        return values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the sum of an input population.")]
    public double sum(List<double> values)
    {
        double sum = 0;

        foreach (double value in values)
            sum += value;

        return sum;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the arithmetic mean of an input population.")]
    public double mean(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sum = 0;

        foreach (double value in values)
            sum += value;

        return sum / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the minimum value in an input population.")]
    public double min(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double min = values[0];

        for (int i = 1; i < values.Count; i++)
            if (values[i] < min)
                min = values[i];

        return min;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the maximum value in an input population.")]
    public double max(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double max = values[0];

        for (int i = 1; i < values.Count; i++)
            if (values[i] > max)
                max = values[i];

        return max;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the median of an input population.")]
    public double median(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double[] sorted = values.ToArray();
        Array.Sort(sorted);

        int middle = sorted.Length / 2;

        if ((sorted.Length & 1) != 0)
            return sorted[middle];

        return (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the population variance of an input population.")]
    public double variance_population(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double average = mean(values);
        double sumSquares = 0;

        foreach (double value in values)
        {
            double delta = value - average;
            sumSquares += delta * delta;
        }

        return sumSquares / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the sample variance of an input population.")]
    public double variance_sample(List<double> values)
    {
        if (values.Count < 2)
            return double.NaN;

        double average = mean(values);
        double sumSquares = 0;

        foreach (double value in values)
        {
            double delta = value - average;
            sumSquares += delta * delta;
        }

        return sumSquares / (values.Count - 1);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the population standard deviation of an input population.")]
    public double stddev_population(List<double> values)
    {
        return Math.Sqrt(variance_population(values));
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the sample standard deviation of an input population.")]
    public double stddev_sample(List<double> values)
    {
        return Math.Sqrt(variance_sample(values));
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the root mean square of an input population.")]
    public double rms(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sumSquares = 0;

        foreach (double value in values)
            sumSquares += value * value;

        return Math.Sqrt(sumSquares / values.Count);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the mean absolute value of an input population.")]
    public double mean_abs(List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sum = 0;

        foreach (double value in values)
            sum += Math.Abs(value);

        return sum / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the population covariance between two input populations.")]
    public double covariance_population(
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count == 0 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(x_values);
        double yMean = mean(y_values);
        double sum = 0;

        for (int i = 0; i < x_values.Count; i++)
            sum += (x_values[i] - xMean) * (y_values[i] - yMean);

        return sum / x_values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the sample covariance between two input populations.")]
    public double covariance_sample(
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count < 2 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(x_values);
        double yMean = mean(y_values);
        double sum = 0;

        for (int i = 0; i < x_values.Count; i++)
            sum += (x_values[i] - xMean) * (y_values[i] - yMean);

        return sum / (x_values.Count - 1);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp("Find the Pearson correlation coefficient between two input populations.")]
    public double pearson(
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count < 2 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(x_values);
        double yMean = mean(y_values);

        double covariance = 0;
        double xSquares = 0;
        double ySquares = 0;

        for (int i = 0; i < x_values.Count; i++)
        {
            double xDelta = x_values[i] - xMean;
            double yDelta = y_values[i] - yMean;

            covariance += xDelta * yDelta;
            xSquares += xDelta * xDelta;
            ySquares += yDelta * yDelta;
        }

        double denominator = Math.Sqrt(xSquares * ySquares);

        if (denominator == 0)
            return double.NaN;

        return Math.Clamp(covariance / denominator, -1.0, 1.0);
    }
}