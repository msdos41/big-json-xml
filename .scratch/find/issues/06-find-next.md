# 06: Find Next

**What to build:** A person can step through hits in document order without using the results list. Find Next reveals the next hit. The first one reveals the first hit. The last one stops. The scan keeps going until that next hit exists.

**Blocked by:** 01: Find what matches values and still reveals

**Status:** ready-for-agent

- [ ] Find Next reveals the next hit in document order on the one document surface.
- [ ] When nothing is the current hit, Find Next reveals the first hit.
- [ ] Find Next on the last hit stops, and does not reveal the first hit again.
- [ ] If the scan has not reached the next hit, Find Next waits for the scan to produce it or to stop.
- [ ] When that hit is already in the results, its row is the current row.
- [ ] Window-model tests cover the first hit, the next hit, the last hit, and the current row.
