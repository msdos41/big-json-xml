# Closing Find UI freezes after large Find All

Status: resolved

## Answer

Avalonia clears `ItemsSource` with Remove of the entire source. With a virtual list Count in the tens/hundreds of thousands that freezes the UI. `ReleaseResultViews` drops Count to 0 before unbind; SyncVisible does not republish while the results window is closed.
