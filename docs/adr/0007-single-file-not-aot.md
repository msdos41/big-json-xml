# Single-file CoreCLR publish, Native AOT not required

The original prompt required Native AOT. v1 is a self-contained single-file executable on CoreCLR instead. AOT remains a non-goal so the viewer can use the runtime without a trimming fight; publish still aims at one file the user can copy and run.
