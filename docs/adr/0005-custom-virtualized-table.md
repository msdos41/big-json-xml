# Custom virtualized table, not TreeDataGrid

The original prompt named Avalonia TreeDataGrid. Current TreeDataGrid is an Avalonia Pro control; the MIT repo is archived. Its hierarchy is tree-rows, which we are not using (ADR-0004). v1 uses a custom virtualized table: row virtualization is required; a sub-table is another table instance. Column virtualization is not a v1 target if a table stays on the order of tens of columns.
