# A 500MB XML scan takes about ten seconds

On a local disk, scanning an XML document of about 500MB finishes in about ten seconds of wall time, and the window keeps accepting input throughout. The scan is still one forward reading of the whole document. JSON stays out of scope (ADR 0002). A larger document may take longer. The scan runs to the end; it is not cut off when ten seconds elapse.

A scan that takes minutes was rejected. Freezing the window until the scan ends was rejected.
