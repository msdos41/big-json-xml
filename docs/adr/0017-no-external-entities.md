# Do not resolve external entities; stop at the first well-formedness error

The viewer does not fetch DTDs, does not read files a DTD points at, and does not open network connections to expand entities. Internal entities are expanded. An external entity reference or any other well-formedness error stops the scan. A bare attribute name is not such an error (ADR 0020). Tables already shown stay on screen, and the error is marked at that offset. The viewer does not repair the rest of the file.

Best-effort recovery was rejected because later entries and hit paths would no longer correspond to the document.
