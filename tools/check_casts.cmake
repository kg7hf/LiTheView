# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_policy(SET CMP0009 NEW)

if(NOT DEFINED LITHEVIEW_ROOT)
    message(FATAL_ERROR "LITHEVIEW_ROOT is required")
endif()

file(GLOB_RECURSE litheview_sources LIST_DIRECTORIES false
    "${LITHEVIEW_ROOT}/include/*.hpp"
    "${LITHEVIEW_ROOT}/src/*.cpp"
    "${LITHEVIEW_ROOT}/resources/*.cpp"
    "${LITHEVIEW_ROOT}/examples/*.hpp"
    "${LITHEVIEW_ROOT}/examples/*.cpp"
    "${LITHEVIEW_ROOT}/tests/*.hpp"
    "${LITHEVIEW_ROOT}/tests/*.cpp"
    "${LITHEVIEW_ROOT}/tools/designer/templates/simulator/*.hpp"
    "${LITHEVIEW_ROOT}/tools/designer/templates/simulator/*.cpp"
)

set(cast_line_count 0)
set(preview_boundary_cast_line_count 0)
set(simulator_boundary_cast_line_count 0)
set(starter_boundary_cast_line_count 0)
set(completed_starter_boundary_cast_line_count 0)
set(h750_menu_boundary_cast_line_count 0)
set(h750_sdr_boundary_cast_line_count 0)
set(l4r9_menu_boundary_cast_line_count 0)
set(tiny_gecko_boundary_cast_line_count 0)
set(tiny_gecko_sensor_cast_line_count 0)
set(tiny_gecko_complete_boundary_cast_line_count 0)
set(tiny_gecko_complete_sensor_cast_line_count 0)
foreach(source IN LISTS litheview_sources)
    file(RELATIVE_PATH relative_source "${LITHEVIEW_ROOT}" "${source}")
    # The consuming starter applications contain real vendor/library submodules and local builds.
    # Audit its application/adapters, never a second checkout or generated build products.
    if(relative_source MATCHES "^examples/[^/]+/(Drivers|Middlewares|build)/")
        continue()
    endif()
    # Old designer exports contain a copy of the Win32 simulator template.
    # Audit its source once below; a developer generating UI must not spend the
    # application's platform-boundary allowance on this unused legacy copy.
    if(relative_source MATCHES "^examples/stm32h750b-dk-(starter-1|starter-1-complete)/Tasks/UI/Generated/win32.cpp$")
        continue()
    endif()
    if(relative_source MATCHES "^src/designer_preview/")
        set(is_preview_translation_boundary ON)
    else()
        set(is_preview_translation_boundary OFF)
    endif()
    file(STRINGS "${source}" source_lines)
    set(previous_line "")
    set(two_lines_back "")
    set(line_number 0)
    foreach(line IN LISTS source_lines)
        math(EXPR line_number "${line_number} + 1")
        if(line MATCHES "(static_cast|reinterpret_cast|const_cast|dynamic_cast|bit_cast)[ \\t]*<")
            if(is_preview_translation_boundary)
                # The native worker is the one reviewed conversion boundary between untyped C ABI /
                # wire fields, Windows handles, and typed C++ core values. Keep its footprint under a
                # separate exact ceiling so it cannot weaken or silently consume the core allowance.
                math(EXPR preview_boundary_cast_line_count
                    "${preview_boundary_cast_line_count} + 1")
            else()
                if(relative_source STREQUAL "examples/efm32tg-stk3300-menu-1-complete/Devices/capacitive_position.hpp")
                    math(EXPR tiny_gecko_complete_sensor_cast_line_count "${tiny_gecko_complete_sensor_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/efm32tg-stk3300-menu-1-complete/Platform/Win32/")
                    math(EXPR tiny_gecko_complete_boundary_cast_line_count "${tiny_gecko_complete_boundary_cast_line_count} + 1")
                elseif(relative_source STREQUAL "examples/efm32tg-stk3300-menu-1/Devices/capacitive_position.hpp")
                    math(EXPR tiny_gecko_sensor_cast_line_count "${tiny_gecko_sensor_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/efm32tg-stk3300-menu-1/Platform/Win32/")
                    math(EXPR tiny_gecko_boundary_cast_line_count "${tiny_gecko_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/stm32h750b-dk-menu-1-complete/")
                    math(EXPR h750_menu_boundary_cast_line_count "${h750_menu_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/stm32h750b-dk-sdr/")
                    math(EXPR h750_sdr_boundary_cast_line_count "${h750_sdr_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/stm32l4r9i-disco-menu-1/")
                    math(EXPR l4r9_menu_boundary_cast_line_count "${l4r9_menu_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/stm32h750b-dk-starter-1-complete/")
                    math(EXPR completed_starter_boundary_cast_line_count "${completed_starter_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^examples/stm32h750b-dk-starter-1/")
                    math(EXPR starter_boundary_cast_line_count "${starter_boundary_cast_line_count} + 1")
                elseif(relative_source MATCHES "^tools/designer/templates/simulator/")
                    math(EXPR simulator_boundary_cast_line_count "${simulator_boundary_cast_line_count} + 1")
                else()
                    math(EXPR cast_line_count "${cast_line_count} + 1")
                endif()
                if(NOT line MATCHES "LITHEVIEW_CAST_BOUNDARY" AND
                   NOT previous_line MATCHES "LITHEVIEW_CAST_BOUNDARY" AND
                   NOT two_lines_back MATCHES "LITHEVIEW_CAST_BOUNDARY")
                    message(FATAL_ERROR
                        "Unreviewed explicit cast at ${relative_source}:${line_number}: ${line}")
                endif()
            endif()
        endif()
        set(two_lines_back "${previous_line}")
        set(previous_line "${line}")
    endforeach()
endforeach()

# The completed lesson repeats the same platform boundaries and one generated Win32 template.
if(tiny_gecko_complete_boundary_cast_line_count GREATER 10 OR tiny_gecko_complete_sensor_cast_line_count GREATER 1)
    message(FATAL_ERROR "Completed Tiny Gecko exceeds its separate 10 Win32 / 1 sensor cast boundary")
endif()
if(tiny_gecko_boundary_cast_line_count GREATER 10)
    message(FATAL_ERROR "Tiny Gecko Win32 cast footprint grew beyond its 10 reviewed boundary lines")
endif()
if(tiny_gecko_sensor_cast_line_count GREATER 1)
    message(FATAL_ERROR "Tiny Gecko sensor normalization exceeds its one bounded narrowing conversion")
endif()
message(STATUS "Tiny Gecko Win32 cast boundary: ${tiny_gecko_boundary_cast_line_count}/10")
if(completed_starter_boundary_cast_line_count GREATER 10)
    message(FATAL_ERROR "Completed H750 starter cast footprint grew to ${completed_starter_boundary_cast_line_count}; reviewed ceiling is 10")
endif()
if(starter_boundary_cast_line_count GREATER 9)
    message(FATAL_ERROR "H750 starter cast footprint grew to ${starter_boundary_cast_line_count}; reviewed ceiling is 9")
endif()

# These are independent application/ABI budgets; they cannot consume Core's allowance.
if(h750_menu_boundary_cast_line_count GREATER 11 OR l4r9_menu_boundary_cast_line_count GREATER 15)
    message(FATAL_ERROR "Menu cast boundary grew: H750=${h750_menu_boundary_cast_line_count}/11, L4R9=${l4r9_menu_boundary_cast_line_count}/15")
endif()
message(STATUS "Reviewed menu cast boundaries: H750=${h750_menu_boundary_cast_line_count}, L4R9=${l4r9_menu_boundary_cast_line_count}")
if(h750_sdr_boundary_cast_line_count GREATER 8)
    message(FATAL_ERROR "H750 SDR platform cast boundary grew beyond 8 reviewed lines")
endif()
message(STATUS "Reviewed H750 SDR platform cast boundary: ${h750_sdr_boundary_cast_line_count}/8")

# Reductions are welcome; growth requires an explicit update and review of this ceiling.
# ListView stores its pressed row in one byte under the 64-item bound. PlotView
# converts a validated cursor index below 4096 to its signed geometry domain.
if(cast_line_count GREATER 17)
    message(FATAL_ERROR
        "Explicit-cast footprint grew to ${cast_line_count} lines; reviewed ceiling is 16")
endif()

# This is deliberately exact rather than a maximum. Adding or removing a conversion site requires
# reviewing the complete ABI/wire translation boundary and updating this ratchet in the same change.
# Native packed preview removed five conversions; segmented replies add two bounded wire-size conversions.
if(NOT preview_boundary_cast_line_count EQUAL 71)
    message(FATAL_ERROR
        "Designer preview translation-boundary cast footprint changed from 71 to "
        "${preview_boundary_cast_line_count}; review the complete boundary")
endif()

# The generated simulator currently needs one Win32 LPARAM-to-MINMAXINFO boundary.
# Keep templates visible to the audit without spending the portable-core allowance.
if(NOT simulator_boundary_cast_line_count EQUAL 1)
    message(FATAL_ERROR
        "Simulator-template cast footprint changed from 1 to ${simulator_boundary_cast_line_count}; review the boundary")
endif()

message(STATUS
    "LitheView cast audit passed: ${cast_line_count} core and "
    "${preview_boundary_cast_line_count} preview and "
    "${simulator_boundary_cast_line_count} simulator and "
    "${starter_boundary_cast_line_count} starter and ${completed_starter_boundary_cast_line_count} completed-starter cast-bearing lines")
