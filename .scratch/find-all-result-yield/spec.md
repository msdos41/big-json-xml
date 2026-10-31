# Find All result list is virtualized

Status: ready-for-human

## Problem

Background search can hand over a huge `Hits` batch in one pump. Materializing every `FindResult` into `ObservableCollection` freezes the UI. Progressive dump of all rows still makes the user wait to reach a far scroll position.

## Solution

- Virtual `IList` for `VisibleResults`: Count = 1 (group) + HitCount; indexer describes on demand.
- Lazy `FindResultList` cache (bounded) behind `Results[i]`.
- Match count / ResultCount / scrollbar track HitCount immediately.
- Esc cancels the search thread; already-found hits remain.
- Avalonia ListBox keeps view virtualization; data virtualization is our responsibility.

## Out of scope

- XmlSkipList for Capital `mgr` sibling roots.
