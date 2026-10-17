# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

foreach(required IN ITEMS
        SIZE_TOOL
        CORE_ARCHIVE
        STACK_ROOT
        MAX_TEXT_BYTES
        MAX_DATA_BYTES
        MAX_BSS_BYTES
        MAX_STACK_FRAME_BYTES
        METRICS_FILE)
    if(NOT DEFINED ${required} OR "${${required}}" STREQUAL "")
        message(FATAL_ERROR "Embedded budget check requires ${required}")
    endif()
endforeach()

set(archives "${CORE_ARCHIVE}")
if(DEFINED DEMO_ARCHIVE AND NOT "${DEMO_ARCHIVE}" STREQUAL "")
    list(APPEND archives "${DEMO_ARCHIVE}")
endif()
if(DEFINED COMPACT_FONT_ARCHIVE AND NOT "${COMPACT_FONT_ARCHIVE}" STREQUAL "")
    list(APPEND archives "${COMPACT_FONT_ARCHIVE}")
endif()
if(DEFINED FONTS_ARCHIVE AND NOT "${FONTS_ARCHIVE}" STREQUAL "")
    list(APPEND archives "${FONTS_ARCHIVE}")
endif()
if(DEFINED PROBE_ARCHIVE AND NOT "${PROBE_ARCHIVE}" STREQUAL "")
    list(APPEND archives "${PROBE_ARCHIVE}")
endif()
if(DEFINED SUPPORT_ARCHIVE AND NOT "${SUPPORT_ARCHIVE}" STREQUAL "")
    list(APPEND archives "${SUPPORT_ARCHIVE}")
endif()
foreach(archive IN LISTS archives)
    if(NOT EXISTS "${archive}")
        message(FATAL_ERROR "Embedded budget archive does not exist: ${archive}")
    endif()
endforeach()

execute_process(
    COMMAND "${SIZE_TOOL}" -t ${archives}
    RESULT_VARIABLE size_result
    OUTPUT_VARIABLE size_output
    ERROR_VARIABLE size_error)
if(NOT size_result EQUAL 0)
    message(FATAL_ERROR
        "Embedded size tool failed (${size_result}): ${size_error}")
endif()

string(REGEX MATCH
    "([0-9]+)[ \t]+([0-9]+)[ \t]+([0-9]+)[ \t]+([0-9]+)[ \t]+[0-9a-fA-F]+[ \t]+\\(TOTALS\\)"
    totals "${size_output}")
if(totals STREQUAL "")
    message(FATAL_ERROR "Embedded size output did not contain a TOTALS row:\n${size_output}")
endif()
set(text_bytes "${CMAKE_MATCH_1}")
set(data_bytes "${CMAKE_MATCH_2}")
set(bss_bytes "${CMAKE_MATCH_3}")

if(text_bytes GREATER MAX_TEXT_BYTES)
    message(FATAL_ERROR
        "Embedded text budget exceeded: ${text_bytes} > ${MAX_TEXT_BYTES} bytes")
endif()
if(data_bytes GREATER MAX_DATA_BYTES)
    message(FATAL_ERROR
        "Embedded initialized-data budget exceeded: ${data_bytes} > ${MAX_DATA_BYTES} bytes")
endif()
if(bss_bytes GREATER MAX_BSS_BYTES)
    message(FATAL_ERROR
        "Embedded archive BSS budget exceeded: ${bss_bytes} > ${MAX_BSS_BYTES} bytes")
endif()

file(GLOB_RECURSE stack_reports LIST_DIRECTORIES FALSE "${STACK_ROOT}/*.su")
if(NOT stack_reports)
    message(FATAL_ERROR
        "No GCC stack-usage reports were generated under ${STACK_ROOT}")
endif()

set(maximum_stack_frame 0)
set(maximum_stack_function "")
foreach(report IN LISTS stack_reports)
    file(STRINGS "${report}" stack_lines)
    foreach(line IN LISTS stack_lines)
        if(line MATCHES "[ \t]([0-9]+)[ \t]+([^ \t\r\n]+)$")
            set(frame_bytes "${CMAKE_MATCH_1}")
            set(stack_kind "${CMAKE_MATCH_2}")
            if(stack_kind STREQUAL "dynamic")
                message(FATAL_ERROR
                    "Unbounded dynamic stack usage reported in ${report}: ${line}")
            endif()
            if(frame_bytes GREATER maximum_stack_frame)
                set(maximum_stack_frame "${frame_bytes}")
                set(maximum_stack_function "${line}")
            endif()
        endif()
    endforeach()
endforeach()

if(maximum_stack_frame GREATER MAX_STACK_FRAME_BYTES)
    message(FATAL_ERROR
        "Embedded stack-frame budget exceeded: ${maximum_stack_frame} > "
        "${MAX_STACK_FRAME_BYTES} bytes (${maximum_stack_function})")
endif()

file(WRITE "${METRICS_FILE}"
    "{\n"
    "  \"archive_text_bytes\": ${text_bytes},\n"
    "  \"archive_data_bytes\": ${data_bytes},\n"
    "  \"archive_bss_bytes\": ${bss_bytes},\n"
    "  \"maximum_stack_frame_bytes\": ${maximum_stack_frame},\n"
    "  \"text_budget_bytes\": ${MAX_TEXT_BYTES},\n"
    "  \"data_budget_bytes\": ${MAX_DATA_BYTES},\n"
    "  \"bss_budget_bytes\": ${MAX_BSS_BYTES},\n"
    "  \"stack_frame_budget_bytes\": ${MAX_STACK_FRAME_BYTES}\n"
    "}\n")

message(STATUS
    "Embedded budgets passed: text=${text_bytes}, data=${data_bytes}, bss=${bss_bytes}, "
    "maximum stack frame=${maximum_stack_frame} bytes")
