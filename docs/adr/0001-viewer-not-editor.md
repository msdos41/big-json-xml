# Viewer first: no in-place edit, no save

The original prompt asked for a XiMpLe-style editor with cell edits and Piece Table saves. We are shipping a viewer: open, table presentation, scroll, search. Writes are a second system (index deltas, validity, undo) and would freeze the read path around an edit model we do not need yet.
