# A hit waits until the scan can open it

Text search and the scan share each slice. Search stops at the byte the scan has reached, including inside one drain of repeated siblings. A hit is published only when the outline already holds every list entry on its path, so reading or revealing it works the moment it appears in Hits. A hit that cannot be read yet holds back the hits after it, so Hits stays in document order and Find Next and Find All see the same sequence they would after the scan. When the scan ends or stops, every remaining hit is published.

A waiting hit is checked again after the scan has moved on, and the distance doubles after each miss, because opening an open entry reads it again from its start. The drain budget stays a wall-clock slice (ADR 0025, ADR 0049); tests may shorten it to pin a slow machine.

Letting search run ahead and throwing when a hit names an entry not yet counted was rejected: it only passed on machines fast enough to finish each drain in one slice. Counting every entry at its start tag was rejected because it changes the scan's hot path (ADR 0044).
