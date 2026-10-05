# A JSON parent is the nearest named field

The parent name of a JSON hit is the nearest field name that contains it, without a list index. An object that is a list entry has no name and is skipped, so inside `{"nums":[{"id":"a1"}]}` the parent of `id` is `nums`. The grandfather is one named field further up. A parent-attribute requirement matches nothing in JSON. Requiring the immediate object to have its own name would make that filter miss every array of objects.
