namespace TruthInTheFlip.Format;

public abstract class MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Get the absolute value of an input.")]
    public static double abs(object sample, double value)
    {
        return Math.Abs(value);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Negate an input value.")]
    public static double negate(object sample, double value)
    {
        return -value;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Square an input value.")]
    public static double square(object sample, double value)
    {
        return value * value;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Get the square root of an input.")]
    public static double sqrt(object sample, double value)
    {
        return Math.Sqrt(value);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Get the natural logarithm of an input.")]
    public static double ln(object sample, double value)
    {
        return Math.Log(value);
    }
    
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Add two values together.")]
    public static double add(object sample, double a, double b)
    {
        return a + b;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Subtract one value from another.")]
    public static double sub(object sample, double a, double b)
    {
        return a - b;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Multiply two values together.")]
    public static double mul(object sample, double a, double b)
    {
        return a * b;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Divide one value by another.")]
    public static double div(object sample, double a, double b)
    {
        return a / b;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Calculate the nth root of a value using reciprocal power.")]
    public static double root(object sample, double value, double n)
    {
        return Math.Pow(value, 1.0 / n);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Raise a value to a power.")]
    public static double pow(object sample, double value, double exponent)
    {
        return Math.Pow(value, exponent);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Add an offset to an input value.")]
    public static double offset(object sample, double value, double amount)
    {
        return value + amount;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Measure an input relative to the 50 percent baseline.")]
    public static double offset50(object sample, double value)
    {
        return value - 50.0;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Multiply an input by a scale factor.")]
    public static double scale(object sample, double value, double factor)
    {
        return value * factor;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Divide one input by another.")]
    public static double ratio(object sample, double numerator, double denominator)
    {
        return numerator / denominator;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Limit an input to the inclusive minimum and maximum.")]
    public static double clamp(object sample, double value, double min, double max)
    {
        return Math.Clamp(value, min, max);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Linearly interpolate between two values.")]
    public static double lerp(object sample, double a, double b, double amount)
    {
        return a + ((b - a) * amount);
    }
}


public abstract class MetricFunctionsAggregate : MetricFunctions
{
    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the number of values in an input population.")]
    public static double count(object sample, List<double> values)
    {
        return values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the sum of an input population.")]
    public static double sum(object sample, List<double> values)
    {
        double sum = 0;

        foreach (double value in values)
            sum += value;

        return sum;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the arithmetic mean of an input population.")]
    public static double mean(object sample, List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sum = 0;

        foreach (double value in values)
            sum += value;

        return sum / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the minimum value in an input population.")]
    public static double min(object sample, List<double> values)
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
    [StringHelp( "Find the maximum value in an input population.")]
    public static double max(object sample, List<double> values)
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
    [StringHelp( "Find the median of an input population.")]
    public static double median(object sample, List<double> values)
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
    [StringHelp( "Find the population variance of an input population.")]
    public static double variance_population(object sample, List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double average = mean(sample, values);
        double sumSquares = 0;

        foreach (double value in values)
        {
            double delta = value - average;
            sumSquares += delta * delta;
        }

        return sumSquares / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the sample variance of an input population.")]
    public static double variance_sample(object sample, List<double> values)
    {
        if (values.Count < 2)
            return double.NaN;

        double average = mean(sample, values);
        double sumSquares = 0;

        foreach (double value in values)
        {
            double delta = value - average;
            sumSquares += delta * delta;
        }

        return sumSquares / (values.Count - 1);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the population standard deviation of an input population.")]
    public static double stddev_population(object sample, List<double> values)
    {
        return Math.Sqrt(variance_population(sample, values));
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the sample standard deviation of an input population.")]
    public static double stddev_sample(object sample, List<double> values)
    {
        return Math.Sqrt(variance_sample(sample, values));
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the root mean square of an input population.")]
    public static double rms(object sample, List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sumSquares = 0;

        foreach (double value in values)
            sumSquares += value * value;

        return Math.Sqrt(sumSquares / values.Count);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the mean absolute value of an input population.")]
    public static double mean_abs(object sample, List<double> values)
    {
        if (values.Count == 0)
            return double.NaN;

        double sum = 0;

        foreach (double value in values)
            sum += Math.Abs(value);

        return sum / values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the population covariance between two input populations.")]
    public static double covariance_population(object sample, 
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count == 0 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(sample, x_values);
        double yMean = mean(sample, y_values);
        double sum = 0;

        for (int i = 0; i < x_values.Count; i++)
            sum += (x_values[i] - xMean) * (y_values[i] - yMean);

        return sum / x_values.Count;
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the sample covariance between two input populations.")]
    public static double covariance_sample(object sample, 
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count < 2 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(sample, x_values);
        double yMean = mean(sample, y_values);
        double sum = 0;

        for (int i = 0; i < x_values.Count; i++)
            sum += (x_values[i] - xMean) * (y_values[i] - yMean);

        return sum / (x_values.Count - 1);
    }

    [IsMetric("TruthInTheFlip.v1.1.0")]
    [StringHelp( "Find the Pearson correlation coefficient between two input populations.")]
    public static double pearson(object sample, 
        List<double> x_values,
        List<double> y_values)
    {
        if (x_values.Count < 2 || x_values.Count != y_values.Count)
            return double.NaN;

        double xMean = mean(sample, x_values);
        double yMean = mean(sample, y_values);

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