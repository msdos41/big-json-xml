# Expansion is inside the cell, and the row grows

Every nested record table or list table is drawn inside the cell that was expanded. That row's height grows to hold it. Drill-in, a second pane, and a new window were rejected. Tree-style indented child rows were also rejected: the embedded table keeps its own headers (vertical for a record, horizontal for a list).

How a row that tall is scrolled and virtualized is not decided. A cell that embeds a million-entry list cannot be realized as a million live controls.

ADR-0022 confines the embedded table to the cell's rectangle and widens that cell to fit it. Sibling cells open and close on their own.
