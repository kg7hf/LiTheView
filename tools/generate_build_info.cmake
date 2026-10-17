# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)

foreach(required_variable
        LITHEVIEW_SOURCE_DIR
        LITHEVIEW_BUILD_INFO_HEADER
        LITHEVIEW_BUILD_INFO_VERSION
        LITHEVIEW_BUILD_INFO_VARIANT)
    if(NOT DEFINED ${required_variable} OR "${${required_variable}}" STREQUAL "")
        message(FATAL_ERROR "Missing required variable: ${required_variable}")
    endif()
endforeach()

function(litheview_escape_cpp_string input output)
    string(REPLACE "\\" "\\\\" escaped "${input}")
    string(REPLACE "\"" "\\\"" escaped "${escaped}")
    string(REPLACE "\r" "" escaped "${escaped}")
    string(REPLACE "\n" " " escaped "${escaped}")
    set(${output} "${escaped}" PARENT_SCOPE)
endfunction()

execute_process(
    COMMAND git -C "${LITHEVIEW_SOURCE_DIR}" symbolic-ref --quiet --short HEAD
    RESULT_VARIABLE branch_result
    OUTPUT_VARIABLE build_branch
    ERROR_QUIET
    OUTPUT_STRIP_TRAILING_WHITESPACE)
if(NOT branch_result EQUAL 0 OR build_branch STREQUAL "")
    set(build_branch "detached")
endif()

execute_process(
    COMMAND git -C "${LITHEVIEW_SOURCE_DIR}" rev-parse --short=12 HEAD
    RESULT_VARIABLE commit_result
    OUTPUT_VARIABLE build_commit
    ERROR_QUIET
    OUTPUT_STRIP_TRAILING_WHITESPACE)
if(NOT commit_result EQUAL 0 OR build_commit STREQUAL "")
    set(build_commit "unknown")
endif()

execute_process(
    COMMAND git -C "${LITHEVIEW_SOURCE_DIR}" status --porcelain
    RESULT_VARIABLE status_result
    OUTPUT_VARIABLE worktree_status
    ERROR_QUIET
    OUTPUT_STRIP_TRAILING_WHITESPACE)
if(status_result EQUAL 0 AND NOT worktree_status STREQUAL "")
    string(APPEND build_commit "+dirty")
endif()

string(TIMESTAMP build_timestamp "%Y-%m-%d %H:%M:%S UTC" UTC)

litheview_escape_cpp_string("VERSION: ${LITHEVIEW_BUILD_INFO_VERSION}" version_text)
litheview_escape_cpp_string("TARGET: ${LITHEVIEW_BUILD_INFO_VARIANT}" variant_text)
litheview_escape_cpp_string("BRANCH: ${build_branch}" branch_text)
litheview_escape_cpp_string("COMMIT: ${build_commit}" commit_text)
litheview_escape_cpp_string("BUILT: ${build_timestamp}" timestamp_text)

set(header_content "#pragma once\n\n")
string(APPEND header_content "#include <string_view>\n\n")
string(APPEND header_content "namespace lithe::demo::buildinfo\n{\n\n")
string(APPEND header_content
    "inline constexpr std::string_view kVersionText{\"${version_text}\"};\n")
string(APPEND header_content
    "inline constexpr std::string_view kVariantText{\"${variant_text}\"};\n")
string(APPEND header_content
    "inline constexpr std::string_view kBranchText{\"${branch_text}\"};\n")
string(APPEND header_content
    "inline constexpr std::string_view kCommitText{\"${commit_text}\"};\n")
string(APPEND header_content
    "inline constexpr std::string_view kTimestampText{\"${timestamp_text}\"};\n\n")
string(APPEND header_content "} // namespace lithe::demo::buildinfo\n")

get_filename_component(output_directory "${LITHEVIEW_BUILD_INFO_HEADER}" DIRECTORY)
file(MAKE_DIRECTORY "${output_directory}")
set(temporary_header "${LITHEVIEW_BUILD_INFO_HEADER}.tmp")
file(WRITE "${temporary_header}" "${header_content}")
execute_process(
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
        "${temporary_header}"
        "${LITHEVIEW_BUILD_INFO_HEADER}"
    COMMAND_ERROR_IS_FATAL ANY)
file(REMOVE "${temporary_header}")
