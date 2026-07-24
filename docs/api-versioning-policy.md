# TMS API Versioning Policy

## 1. Breaking Changes
The following changes are classified as breaking and require a new API major version (e.g., V1 to V2):
- Removing or renaming fields/properties in requests or responses.
- Changing data types or standard status code responses.
- Tightening validation rules.
- Changing default sorting or mandatory query behavior.

## 2. Non-Breaking (Additive) Changes
The following changes are backwards-compatible and can be added to the active version:
- Adding optional properties to response payloads.
- Adding optional query string parameters or headers.
- Adding entirely new API endpoints.

## 3. Sunset Window
- Deprecated versions will remain fully supported for a minimum of **6 months** after a successor version is released to accommodate remote/offline clients.

## 4. Communication Strategy
- **HTTP Headers:** `Deprecation`, `Sunset`, and `Link` (successor-version) headers attached to all deprecated endpoint responses from day one.
- **Documentation:** Updates in the API changelog and notification sent to active client integration owners.

## 5. Version Skipping
- Clients are permitted to upgrade directly (e.g., from V1 to V3) without having to migrate through intermediate versions.