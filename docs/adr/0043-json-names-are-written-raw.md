# JSON field names are written raw in the path

A key is the decoded name, including an empty key and a key that contains `/` or brackets. The path writes that name as one step and is not parsed again. An empty key is a blank header, and the file still opens. Escaping those names, or stopping the scan on them, was rejected.
