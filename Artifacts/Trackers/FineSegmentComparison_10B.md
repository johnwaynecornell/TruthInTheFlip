# Fine-Grained 10B Segment Comparison

## Purpose

This note summarizes fine-grained `segment_report` results across the mature tracker artifacts using a **10B rolling tracker window** and **10B segmentation**.

The source reports were generated from `Artifacts/Trackers` with:

```bash
ls *.tkr | xargs -I {} sh -c \
'TruthInTheFlip_Farm_Experimental segment_report All full window by_total 10B file {} full by_total 10B > ~/TrackerTemp/{}.seg_report_fine.txt'
```

The complete generated reports are intentionally treated as analysis/archive artifacts rather than normal repository content because of their size. This file preserves the compact comparison.

## Comparison

| Tracker | Segments | Edge Excursion | Edge Settlement | Persistence Index | End TrueZ >= 0 | Best TrueZ >= 1.96 | Retained Anticipation | Settlement Adjusted |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| `crypto_RandomSD.tkr` | 1,358 | +0.488120 | -0.788651 | -0.399842 | 25.5523% | 5.0810% | 50+2.61953e-04% | 50+3.84499e-04% |
| `crypto3.tkr` | 890 | +0.567330 | -0.757106 | -0.385103 | 26.7416% | 5.5056% | 50+2.79470e-04% | 50+3.71325e-04% |
| `Quant.tkr` | 889 | +0.453900 | -0.847855 | -0.415459 | 24.0720% | 4.4994% | 50+2.47848e-04% | 50+4.15052e-04% |
| `Quant_IDQE.tkr` | 889 | +0.432600 | -0.780441 | -0.389325 | 23.1721% | 5.6243% | 50+2.68012e-04% | 50+3.83083e-04% |
| `Quant2.tkr` | 889 | +0.497440 | -0.785730 | -0.403648 | 24.4094% | 5.6243% | 50+2.60774e-04% | 50+3.90438e-04% |
| `SamePersistence.NET1.tkr` | 894 | +0.566230 | -0.719693 | -0.384303 | 27.9642% | 6.0403% | 50+3.04381e-04% | 50+4.31569e-04% |

## Correlation Geometry

| Tracker | corr(mean Heads Z, mean TrueZ) | corr(end Heads Z, end TrueZ) | corr(mean A, mean Heads Z) | corr(end A, end Heads Z) | corr(mean A, mean TrueZ) | corr(end A, end TrueZ) |
|---|---:|---:|---:|---:|---:|---:|
| `crypto_RandomSD.tkr` | +0.031966 | +0.012287 | +0.030258 | +0.019734 | +0.890473 | +0.846317 |
| `crypto3.tkr` | -0.038075 | -0.046574 | +0.020739 | -0.034991 | +0.919546 | +0.870611 |
| `Quant.tkr` | +0.123610 | +0.087451 | +0.099696 | +0.051038 | +0.890841 | +0.865603 |
| `Quant_IDQE.tkr` | +0.013699 | +0.021443 | -0.043348 | -0.017880 | +0.887515 | +0.848429 |
| `Quant2.tkr` | +0.019087 | +0.041843 | -0.006599 | +0.029739 | +0.902625 | +0.849035 |
| `SamePersistence.NET1.tkr` | -0.041535 | -0.066372 | -0.002103 | -0.046146 | +0.891976 | +0.849625 |

## Reading

Across all six trackers, the fine-grained geometry has a strong family resemblance:

- median excursion is positive while mean settlement is negative;
- only about one quarter of 10B segments finish with non-negative TrueZ;
- anticipation/TrueZ coupling is strong;
- anticipation/Heads coupling remains small.

`SamePersistence.NET1.tkr` fits this family rather than standing apart from it. In this comparison it has the least-negative settlement score, the highest fraction of non-negative settlements, the highest retained anticipation, and the highest settlement-adjusted anticipation. Its excursion score is also essentially the same as `crypto3.tkr`.

This should not be read as proof of a permanent directional edge. The recurring pattern is better described as **local excursion with unfavorable settlement**, consistent with regime-like or relational structure rather than a stable one-way bias.

The mature `SamePersistence.NET1.tkr` result therefore adds a useful new observation: a predictor driven directly by the source's Same/Different balance produces fine-scale geometry that closely resembles the established tracker family while remaining largely decoupled from Heads/Tails balance.

## Status

`SamePersistence` remains an exploratory strategy. Its mature NET1 run is useful for characterization and comparison, but it was not a prospective validation of a frozen hypothesis selected before observing this record.

The next natural comparison is `BetSamePersistence2`, which restores an explicit inner anticipation strategy so that windowed `BetSameWinRate` is derived from the bettor whose performance is being measured. Running it under the same source and 10B window geometry will provide a direct architectural and empirical comparison with `SamePersistence`.
