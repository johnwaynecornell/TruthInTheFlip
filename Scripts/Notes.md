# Scripts Notes

This directory contains analysis and evaluation scripts used during the
TruthInTheFlip development and validation work.

This file is intentionally brief. It is a map, not a complete history.

## Anticipation evaluator progression

The `TrackerAnticipationEvaluator*` scripts preserve the sequence of the
anticipation experiments rather than replacing earlier stages.

### TrackerAnticipationEvaluator.py

Original chronological 70/30 holdout evaluator.

Established the first held-out comparison between a persistence baseline
and the experimental anticipation state.

### TrackerAnticipationEvaluator_AnticipatedPercentage.py

Expanding walk-forward version using future `AnticipatedPercentage`
as the target.

Introduced the five chronological expanding folds used by the later
evaluators.

### TrackerAnticipationEvaluator_BetSameWinRate.py

Changes the future target to `BetSameWinRate`.

This moves the evaluation closer to the actual Same/Different outcome
surface while retaining the same walk-forward structure.

### TrackerAnticipationEvaluator_BetSameResidual.py

Residual and partial-correlation diagnostics.

Used to test whether `BetSameGapTrend` contained forward information
beyond simple persistence of the current BetSame edge.

### TrackerAnticipationEvaluator_BetSameDecision.py

Converts strictly out-of-sample future-edge predictions into explicit
Same/Different decisions.

This is the first evaluator in the series that scores the forecast as a
decision rather than only as a regression result.

### TrackerAnticipationEvaluator_BetSameDecisionZ.py

Adds flip-weighted realized accuracy and cumulative nominal binomial Z
to the decision evaluator.

This exposed the strong persistence-only forward result across the four
development trackers.

### TrackerAnticipationEvaluator_BetSameDecisionControl.py

Circular-shift control.

Keeps the fitted walk-forward decisions fixed while shifting the realized
future edge within each held-out fold, breaking exact temporal alignment
without retraining the model.

### TrackerAnticipationEvaluator_BetSameDecisionBlockControl.py

Final development-side structural control.

Preserves consecutive groups of four 10B segments and randomly permutes
those blocks within each held-out fold.

The persistence-only forward rule survived this control across all four
development trackers.

## Frozen prospective rule

The development work ultimately reduced to the simpler persistence rule:

    FutureBetSameEdge ~ CurrentBetSameEdge

    predicted edge >= 0 -> Same
    predicted edge <  0 -> Different

with:

    segment size: 10B flips
    lag:          1 segment

`BetSameGapTrend` remained interesting as a forecasting variable, but its
additional decision-level advantage was not established by the structural
controls.

## Quant2

`Quant2.tkr` was intentionally excluded from development, candidate
selection, thresholding, control design, and model tuning.

It is reserved for prospective validation of the already-frozen rule.

### TrackerAnticipationEvaluator_BetSameDecisionBlockControl2.py

Prospective Quant2 validation descendant of the frozen block-control evaluator.

The methodology was left unchanged and `Quant2.tkr` was added only after
the persistence rule, walk-forward design, lag, segment size, and structural
control had been frozen.

Quant2 reproduced the persistence result at approximately cumulative
Z +6.9 while again remaining far beyond the 10,000-trial block-permutation
control distribution.

## Related files

Most evaluator stages have corresponding `.txt` output files preserving
the result of that stage.

The `.sh` files are convenience launchers for their corresponding Python
scripts.

`Loader.py` contains shared tracker-loading support used by the Python
analysis scripts.