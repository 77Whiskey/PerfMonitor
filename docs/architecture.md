# Architecture Notes

## Design Goals

- Keep sim IO and Airbus logic separate.
- Minimize latency and GC pressure on the managed side.
- Make the shared-memory contract explicit and versioned.
- Support future modules without coupling them to MSFS transport details.

## Shared-Memory Contract

The producer and consumer share a packed C-style buffer with:

- `BufferHeader`
- `AircraftState`
- `AutopilotFma`
- `EngineData`
- `ControlSurfacePositions`

The header includes `version`, `size`, `sequence`, and `timestamp`. The producer uses an odd/even sequence pattern:

1. Increment sequence to an odd value.
2. Write the payload.
3. Compute checksum.
4. Increment sequence again to publish an even value.

The reader accepts a snapshot only when the pre-read and post-read sequence values match and are even.

## WASM Boundary

The gauge-side code is written as if it can call a publisher directly, but the actual transport is deliberately isolated:

- In local native harness builds, the publisher uses `CreateFileMappingW` and `MapViewOfFile`.
- In true WASM packaging, the same publisher boundary can be redirected to a supported bridge mechanism without rewriting the telemetry extraction logic.

That separation is intentional. It keeps the avionics-specific scrape code stable while the transport adapts to MSFS packaging limits.

## Airbus Modules

`PerformanceEngine` accepts `IAirbusModule` implementations and evaluates them against `FlightSnapshot` domain records.

Current module set:

- `StabilizedApproachModule`
- `LandingPerformanceModule`
- `EcoCruiseModule`

The stabilized approach module is stateful so it can evaluate the first descent through the 1000 ft and 500 ft AGL gates and avoid repeated alerts on every subsequent frame.

## Performance Reference Data

The performance data service loads FCOM-derived JSON tables asynchronously and performs trilinear interpolation across:

- Gross weight
- Outside air temperature
- Pressure altitude

The reference file is intentionally small and illustrative. Replace it with validated airline-engineering data before operational use.