## What is a Z-Score?

A Z-score measures exactly how many standard deviations an observed
result is from the expected mean. In the context of a binary, perfectly
random sequence (like a coin flip), the expected mean win rate `(p₀)` is
always exactly 0.5 (50%).

The Z-score calculates the probability that an observed edge happened by
pure chance. The higher the Z-score, the closer that probability drops
to zero.

## The Formula

To calculate the Z-score for a binary outcome, we use the standard score
formula for a sample proportion:

`Z = (p̂ - p₀) / √[ (p₀ * (1 - p₀)) / n ]`

- `p̂` : The actual observed win rate (e.g., 0.50000032)

- `p₀` : The expected random win rate (0.5)

- `n` : The total number of flips processed

## Interpreting the Score (The Thresholds)

In scientific and statistical literature, specific Z-score thresholds
are used to establish confidence intervals:

- `Z < 1.0` (Normal Variance): The result is resting comfortably within
  standard random noise. No edge can be claimed.

- `Z ≥ 1.96` (Statistical Significance): This represents a 95%
  confidence interval. The odds of achieving this result by pure chance
  are roughly 1 in 20. This is the minimum threshold to suggest an
  anomaly exists.

- `Z ≥ 3.00` (Definitive Proof): This represents a 99.7% confidence
  interval. At this level, the result is mathematically undeniable. The
  odds of the system naturally producing this edge are astronomically
  low.

## The Weight of Sample Size (n)

In massive datasets, even a microscopic edge becomes statistically
significant if it remains consistent. Because the denominator of the
Z-score formula shrinks as \'n\' (total flips) grows, the required
sample size to prove an edge scales exponentially based on how thin that
edge is.

If an algorithm maintains a microscopic edge of just 0.000032% (a win
rate of 50.000032%), we can algebraically invert the Z-score formula to
find exactly how many flips (n) are required to reach the definitive Z =
3.00 threshold:

`n = (Z² * p₀ * (1 - p₀)) / (p̂ - p₀)²`

This inverse calculation is why true statistical proof often requires
hundreds of billions, or even trillions, of processed events to separate
a genuine algorithmic advantage from the natural ghosts of random
variance.