cmake_minimum_required(VERSION 3.26)

project(FenixFpm LANGUAGES CXX)

option(FPM_BUILD_WASM "Build the MSFS probe module" ON)
option(FPM_BUILD_NATIVE_BRIDGE "Build the native shared-memory harness" ON)

if(FPM_BUILD_WASM)
    add_subdirectory(src/Probe.Wasm)
endif()

if(FPM_BUILD_NATIVE_BRIDGE)
    add_subdirectory(src/Bridge.Native)
endif()
