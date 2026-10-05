# XML before JSON

The original prompt treated XML and JSON as one table product. They do not share a table definition (XML has attributes, mixed content, and namespaced names; JSON has arrays and mixed-type lists). v1 opened XML only so the XML table rule could settle first.

ADR-0029 supersedes the wait. A JSON document can be opened. The two formats still do not share one table rule.
