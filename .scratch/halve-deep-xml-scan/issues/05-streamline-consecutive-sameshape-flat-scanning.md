# 05: Streamline consecutive SameShape flat scanning

**What to build:** Flat element scanning bypasses duplicate attribute span checking and redundant text conversions when consecutive entries share the exact same shape. In `ConsumeFlat`, when `SameShape` is true, the quadratic duplicate attribute verification over `_attrSpans` is skipped, relying on the attribute uniqueness already guaranteed when the shape was pinned. The throughput of `DrainFlat` increases across all documents with repeated lists without altering column order or field values.

**Blocked by:** 04: Halve repeated micro-lists with cross-parent shape prediction.

**Status:** resolved

- [x] When `SameShape` is true in `ConsumeFlat`, `_attrSpans` clearing, duplicate span checking, and intermediate attribute allocations are bypassed.
- [x] Scanning a repeated list with many attributes per entry finishes in less time than before.
- [x] Field columns, order, and attribute values remain identical to today's output.
- [x] Existing timing documents (such as `employees-1-GB.xml` and `.vxf` documents) show no performance regression and maintain pinned outlines.
