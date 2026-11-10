# Vehicle search sort state

Vehicle search reads the existing closure sort state shared with sorting and regular reload, instead of calling an unavailable `getCurrentSort` provider.
Filter serialization preserves ordered repeated keys; the existing public SearchPartial request and sort parameters remain in use.
Focused check: `node --test Firebase.Tests/VehicleSearchSort.test.cjs`.
