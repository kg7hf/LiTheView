# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)

set(LITHEVIEW_VERSION 0.4.0)
set(LITHEVIEW_IS_TOP_LEVEL OFF)
if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    set(LITHEVIEW_IS_TOP_LEVEL ON)
    project(LitheView VERSION ${LITHEVIEW_VERSION} LANGUAGES CXX)
endif()

if(LITHEVIEW_IS_TOP_LEVEL)
    set(CMAKE_CXX_STANDARD 23)
    set(CMAKE_CXX_STANDARD_REQUIRED ON)
    set(CMAKE_CXX_EXTENSIONS OFF)
endif()

include(CMakePackageConfigHelpers)
include(GNUInstallDirs)
include(cmake/Segments.cmake)

option(LITHEVIEW_BUILD_RENDERING_FIXTURES "Build internal rendering regression fixtures" ${LITHEVIEW_IS_TOP_LEVEL})
set(litheview_win32_default OFF)
if(LITHEVIEW_BUILD_RENDERING_FIXTURES AND WIN32)
    set(litheview_win32_default ON)
endif()
option(LITHEVIEW_BUILD_WIN32 "Build Win32 rendering inspection tools" ${litheview_win32_default})
set(LITHEVIEW_DESTINATION_FORMAT "ALL" CACHE STRING
    "Compiled destination writers: ALL, RGB565, RGB888, XRGB8888, ARGB8888, R2_PACKED2X2, RRRR2222, or RYRY2222")
set_property(CACHE LITHEVIEW_DESTINATION_FORMAT PROPERTY STRINGS
    ALL
    RGB565
    RGB888
    XRGB8888
    ARGB8888
    R2_PACKED2X2
    RRRR2222
    RYRY2222)
option(LITHEVIEW_BUILD_TESTS "Build portable library tests" ${LITHEVIEW_IS_TOP_LEVEL})
set(litheview_rendering_tests_default OFF)
if(LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_FIXTURES)
    set(litheview_rendering_tests_default ON)
endif()
option(LITHEVIEW_BUILD_RENDERING_TESTS "Build rendering-scene and probe regression tests"
    ${litheview_rendering_tests_default})
option(LITHEVIEW_BUILD_COMPACT_FONT "Provide the optional LitheView::CompactFont resource" ON)
if(LITHEVIEW_BUILD_TESTS)
    enable_testing()
endif()
set(litheview_designer_preview_default ${LITHEVIEW_IS_TOP_LEVEL})
if(CMAKE_CROSSCOMPILING)
    set(litheview_designer_preview_default OFF)
endif()
option(LITHEVIEW_BUILD_DESIGNER_PREVIEW
    "Build the host-only native designer preview ABI and worker"
    ${litheview_designer_preview_default})
option(LITHEVIEW_BUILD_STM32U545_PROBE
    "Build the displayless, strip-rendered NUCLEO-U545RE-Q graphics probe"
    ${LITHEVIEW_IS_TOP_LEVEL})
option(LITHEVIEW_DISABLE_EXCEPTIONS_RTTI
    "Build the portable core without C++ exceptions or run-time type information" ON)
option(LITHEVIEW_WARNINGS_AS_ERRORS
    "Treat first-party compiler warnings as build failures"
    ${LITHEVIEW_IS_TOP_LEVEL})
option(LITHEVIEW_ENABLE_EMBEDDED_BUDGET_GATE
    "Check generic Arm archive and stack-frame budgets"
    ${LITHEVIEW_IS_TOP_LEVEL})
set(LITHEVIEW_EMBEDDED_STACK_FRAME_LIMIT 4096 CACHE STRING
    "Maximum statically bounded first-party stack frame in embedded builds")

if(NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "ALL" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "RGB565" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "RGB888" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "XRGB8888" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "ARGB8888" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "R2_PACKED2X2" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "RRRR2222" AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "RYRY2222")
    message(FATAL_ERROR
        "LITHEVIEW_DESTINATION_FORMAT must be ALL, RGB565, RGB888, XRGB8888, ARGB8888, R2_PACKED2X2, RRRR2222, or RYRY2222")
endif()

foreach(litheview_destination IN ITEMS RGB565 RGB888 XRGB8888 ARGB8888 R2_PACKED2X2 RRRR2222 RYRY2222)
    set(litheview_${litheview_destination}_enabled false)
    if(LITHEVIEW_DESTINATION_FORMAT STREQUAL "ALL" OR
       LITHEVIEW_DESTINATION_FORMAT STREQUAL "${litheview_destination}")
        set(litheview_${litheview_destination}_enabled true)
    endif()
endforeach()

if((LITHEVIEW_BUILD_TESTS OR LITHEVIEW_BUILD_DESIGNER_PREVIEW) AND
   NOT LITHEVIEW_DESTINATION_FORMAT STREQUAL "ALL")
    message(FATAL_ERROR
        "The existing regression tests and designer preview require "
        "LITHEVIEW_DESTINATION_FORMAT=ALL. For a fixed-format product, set "
        "LITHEVIEW_BUILD_TESTS=OFF and LITHEVIEW_BUILD_DESIGNER_PREVIEW=OFF "
        "in a separate build directory.")
endif()

configure_file(
    cmake/render_config.hpp.in
    ${CMAKE_CURRENT_BINARY_DIR}/generated/lithe/render_config.hpp
    @ONLY)
message(STATUS "LitheView destination writers: ${LITHEVIEW_DESTINATION_FORMAT}")

if(LITHEVIEW_BUILD_RENDERING_FIXTURES OR (LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_TESTS))
    include(cmake/rendering_fixtures.cmake)
endif()

function(litheview_configure_first_party_target target_name)
    if(MSVC)
        target_compile_options(${target_name} PRIVATE /W4 /permissive-)
        if(LITHEVIEW_WARNINGS_AS_ERRORS)
            target_compile_options(${target_name} PRIVATE /WX)
        endif()
    else()
        target_compile_options(${target_name} PRIVATE
            -Wall
            -Wextra
            -Wpedantic
            -Wconversion
            -Wsign-conversion
            -Wdouble-promotion
            -Wold-style-cast)
        if(CMAKE_CROSSCOMPILING AND CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
            target_compile_options(${target_name} PRIVATE -ffreestanding)
        endif()
        if(LITHEVIEW_WARNINGS_AS_ERRORS)
            target_compile_options(${target_name} PRIVATE -Werror)
        endif()
        if(CMAKE_CROSSCOMPILING AND CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
            target_compile_options(${target_name} PRIVATE
                -fstack-usage
                -Wstack-usage=${LITHEVIEW_EMBEDDED_STACK_FRAME_LIMIT})
        endif()
    endif()
endfunction()

add_custom_target(litheview_cast_audit ALL
    COMMAND ${CMAKE_COMMAND}
        -DLITHEVIEW_ROOT=${CMAKE_CURRENT_SOURCE_DIR}
        -P ${CMAKE_CURRENT_SOURCE_DIR}/tools/check_casts.cmake
    VERBATIM
)

add_library(litheview_core STATIC
    src/application.cpp
    src/control_view.cpp
    src/controller.cpp
    src/display.cpp
    src/gesture.cpp
    src/generated_font.cpp
    src/memory_budget.cpp
    src/painter.cpp
    src/tiled_renderer.cpp
    src/view.cpp
    src/widgets.cpp
    src/drop_down.cpp
    src/xml_layout.cpp
    src/xml_writer.cpp
)
add_library(LitheView::Core ALIAS litheview_core)
add_dependencies(litheview_core litheview_cast_audit)
set_target_properties(litheview_core PROPERTIES EXPORT_NAME Core)
target_compile_features(litheview_core PUBLIC cxx_std_23)
target_include_directories(litheview_core
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/generated>
        $<INSTALL_INTERFACE:${CMAKE_INSTALL_INCLUDEDIR}>
)
litheview_configure_first_party_target(litheview_core)
if(LITHEVIEW_DISABLE_EXCEPTIONS_RTTI)
    # RTTI is an ABI property for the public polymorphic view hierarchy.  A
    # consumer that derives a view with RTTI enabled can otherwise emit type
    # information that refers to base-class type information intentionally
    # omitted from litheview_core.  Propagate the selected embedded contract to
    # every target that consumes the public headers.
    if(MSVC)
        target_compile_options(litheview_core PUBLIC
            $<$<COMPILE_LANGUAGE:CXX>:/GR->
            $<$<COMPILE_LANGUAGE:CXX>:/EHs-c->)
    else()
        target_compile_options(litheview_core PUBLIC
            $<$<COMPILE_LANGUAGE:CXX>:-fno-exceptions>
            $<$<COMPILE_LANGUAGE:CXX>:-fno-rtti>)
    endif()
endif()

set(litheview_generated_fonts_default OFF)
if((LITHEVIEW_BUILD_RENDERING_FIXTURES AND litheview_resolved_demo_font_backend STREQUAL "GENERATED_SANS") OR
   (LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_TESTS))
    set(litheview_generated_fonts_default ON)
endif()
option(LITHEVIEW_BUILD_GENERATED_FONTS "Provide the optional LitheView::GeneratedFonts resources"
    ${litheview_generated_fonts_default})
add_subdirectory(resources/fonts)

if((LITHEVIEW_BUILD_RENDERING_FIXTURES OR LITHEVIEW_BUILD_TESTS OR LITHEVIEW_BUILD_DESIGNER_PREVIEW)
   AND NOT TARGET LitheView::CompactFont)
    message(FATAL_ERROR "Rendering fixtures, tests and preview use LitheView::CompactFont. Enable LITHEVIEW_BUILD_COMPACT_FONT.")
endif()
if(((LITHEVIEW_BUILD_RENDERING_FIXTURES AND litheview_resolved_demo_font_backend STREQUAL "GENERATED_SANS") OR
    (LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_TESTS)) AND NOT TARGET LitheView::GeneratedFonts)
    message(FATAL_ERROR "The selected showcase or conformance suite needs LITHEVIEW_BUILD_GENERATED_FONTS=ON.")
endif()

if(LITHEVIEW_BUILD_DESIGNER_PREVIEW)
    add_library(litheview_designer_preview STATIC
        src/designer_preview/preview.cpp)
    add_library(LitheView::DesignerPreview ALIAS litheview_designer_preview)
    target_compile_features(litheview_designer_preview PUBLIC cxx_std_23)
    target_link_libraries(litheview_designer_preview PUBLIC LitheView::Core LitheView::CompactFont)
    target_include_directories(litheview_designer_preview PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>)
    litheview_configure_first_party_target(litheview_designer_preview)

    # Windows returns frames through designer-owned shared memory; other hosts return them
    # inline on the worker's stdout.
    add_executable(litheview_designer_preview_worker
        src/designer_preview/worker_main.cpp)
    target_link_libraries(litheview_designer_preview_worker PRIVATE
        LitheView::DesignerPreview)
    litheview_configure_first_party_target(litheview_designer_preview_worker)

    if(LITHEVIEW_BUILD_TESTS)
        add_executable(litheview_designer_preview_tests
            src/designer_preview/preview_tests.cpp)
        target_link_libraries(litheview_designer_preview_tests PRIVATE
            LitheView::DesignerPreview)
        litheview_configure_first_party_target(litheview_designer_preview_tests)
        add_test(NAME litheview_designer_preview_tests
            COMMAND litheview_designer_preview_tests)
    endif()
endif()

if(LITHEVIEW_BUILD_RENDERING_FIXTURES OR (LITHEVIEW_BUILD_TESTS AND LITHEVIEW_BUILD_RENDERING_TESTS))
    set(litheview_build_info_header
        "${CMAKE_CURRENT_BINARY_DIR}/generated/litheview/build_info.hpp")
    add_custom_target(litheview_build_info
        COMMAND ${CMAKE_COMMAND}
            -DLITHEVIEW_SOURCE_DIR=${CMAKE_CURRENT_SOURCE_DIR}
            -DLITHEVIEW_BUILD_INFO_HEADER=${litheview_build_info_header}
            -DLITHEVIEW_BUILD_INFO_VERSION=${LITHEVIEW_VERSION}
            -DLITHEVIEW_BUILD_INFO_VARIANT=${litheview_build_info_variant}
            -P ${CMAKE_CURRENT_SOURCE_DIR}/tools/generate_build_info.cmake
        BYPRODUCTS ${litheview_build_info_header}
        VERBATIM)
    configure_file(
        cmake/demo_font_config.hpp.in
        ${CMAKE_CURRENT_BINARY_DIR}/generated/litheview/demo_font_config.hpp
        @ONLY)
    function(litheview_add_demo_library target_name source_file)
        add_library(${target_name} STATIC ${source_file})
        add_dependencies(${target_name} litheview_build_info)
        target_link_libraries(${target_name} PUBLIC LitheView::Core LitheView::CompactFont)
        if(TARGET LitheView::GeneratedFonts)
            target_link_libraries(${target_name} PUBLIC LitheView::GeneratedFonts)
        endif()
        target_include_directories(${target_name} PUBLIC
            $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/tests/rendering/common>
            $<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/generated>)
        litheview_configure_first_party_target(${target_name})
    endfunction()

    # Each acceptance profile is a separate archive. An embedded consumer pays
    # only for the selected demo's object code.
    litheview_add_demo_library(litheview_demo_common tests/rendering/common/demo.cpp)
    litheview_add_demo_library(litheview_round_demo tests/rendering/common/round_demo.cpp)

    # Compile a checked designer output as an independent archive on every host,
    # Cortex-M33, and Cortex-M7 gate without pulling it into consumer firmware.
    add_library(litheview_generated_form_smoke STATIC
        tests/designer-fixtures/document-compiler-smoke/generated/main_form.cpp)
    target_link_libraries(litheview_generated_form_smoke PUBLIC LitheView::Core)
    target_include_directories(litheview_generated_form_smoke PUBLIC
        tests/designer-fixtures/document-compiler-smoke/generated)
    litheview_configure_first_party_target(litheview_generated_form_smoke)
    add_library(litheview_generated_button_form_smoke STATIC
        tests/designer-fixtures/button-variants-smoke/generated/button_variants.cpp)
    target_link_libraries(litheview_generated_button_form_smoke PUBLIC LitheView::Core)
    target_include_directories(litheview_generated_button_form_smoke PUBLIC
        tests/designer-fixtures/button-variants-smoke/generated)
    litheview_configure_first_party_target(litheview_generated_button_form_smoke)
    add_library(litheview_generated_tab_form_smoke STATIC
        tests/designer-fixtures/tab-view-smoke/generated/tab_settings.cpp)
    target_link_libraries(litheview_generated_tab_form_smoke PUBLIC LitheView::Core)
    target_include_directories(litheview_generated_tab_form_smoke PUBLIC
        tests/designer-fixtures/tab-view-smoke/generated)
    litheview_configure_first_party_target(litheview_generated_tab_form_smoke)
    add_library(litheview_generated_wheel_form_smoke STATIC
        tests/designer-fixtures/wheel-picker-smoke/generated/alarm_time.cpp)
    target_link_libraries(litheview_generated_wheel_form_smoke PUBLIC LitheView::Core)
    target_include_directories(litheview_generated_wheel_form_smoke PUBLIC
        tests/designer-fixtures/wheel-picker-smoke/generated)
    litheview_configure_first_party_target(litheview_generated_wheel_form_smoke)
endif()

if(LITHEVIEW_BUILD_RENDERING_FIXTURES AND LITHEVIEW_BUILD_WIN32)
    add_subdirectory(tests/rendering/win32)
endif()

if(LITHEVIEW_BUILD_RENDERING_FIXTURES AND LITHEVIEW_BUILD_STM32U545_PROBE)
    add_subdirectory(tests/rendering/stm32u545re-q)
endif()

if(LITHEVIEW_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

if(LITHEVIEW_ENABLE_EMBEDDED_BUDGET_GATE AND
   CMAKE_CROSSCOMPILING AND
   CMAKE_SIZE)
    set(litheview_control_events_text_allowance 3000)
    # Text transactions add 1,504 bytes over a2d9056 with Arm GCC 14.3.1.
    # Keep this feature cost separate; RAM and application budgets stay unchanged.
    set(litheview_text_events_text_allowance 1600)
    # Drop-down interaction and root popup composition add about 5.4 KiB to the
    # complete archive sum over c06b623 (Arm GCC 14.3.1). Unused control object
    # code is not a linked application requirement. Keep existing RAM ceilings.
    set(litheview_drop_down_text_allowance 5600)
    # Packed-only Core configurations have no board/application fixture. Keep
    # their archive and stack checks independent of an application transport.
    if(NOT LITHEVIEW_BUILD_RENDERING_FIXTURES)
        add_custom_target(litheview_embedded_core_budget ALL
            COMMAND ${CMAKE_COMMAND}
                -DSIZE_TOOL=${CMAKE_SIZE}
                -DCORE_ARCHIVE=$<TARGET_FILE:litheview_core>
                -DSTACK_ROOT=${CMAKE_BINARY_DIR}
                -DMAX_TEXT_BYTES=68000
                -DMAX_DATA_BYTES=256
                -DMAX_BSS_BYTES=256
                -DMAX_STACK_FRAME_BYTES=${LITHEVIEW_EMBEDDED_STACK_FRAME_LIMIT}
                -DMETRICS_FILE=${CMAKE_BINARY_DIR}/litheview_embedded_core_metrics.json
                -P ${CMAKE_CURRENT_SOURCE_DIR}/tools/check_embedded_budget.cmake
            DEPENDS litheview_core
            VERBATIM)
    endif()
    if(TARGET litheview_demo_common AND TARGET litheview_stm32u545_probe)
    if(litheview_resolved_demo_font_backend STREQUAL "GENERATED_SANS")
        set(litheview_embedded_max_text_bytes 105000)
    else()
        set(litheview_embedded_max_text_bytes 75000)
    endif()
    # Typed control events/group ownership add 2,638 bytes to the Cortex-M33
    # aggregate archive baseline (74,942 -> 77,580, GCC 14.3.1, compact font).
    # This explicit feature allowance is a regression ceiling, not MCU placement
    # evidence. Per-control RAM ceilings and application target budgets stay fixed.
    math(EXPR litheview_embedded_max_text_bytes
        "${litheview_embedded_max_text_bytes} + ${litheview_control_events_text_allowance} + ${litheview_text_events_text_allowance} + ${litheview_drop_down_text_allowance}")
    if(litheview_resolved_demo_animation_format STREQUAL "INDEXED4")
        # The 96x72, eight-frame indexed4 payload is 20,736 bytes larger than alpha1. Keep that
        # deliberate example-resource cost separate from the font/backend baseline instead of
        # weakening compact alpha1 builds with one blanket ceiling.
        math(EXPR litheview_embedded_max_text_bytes
            "${litheview_embedded_max_text_bytes} + 21000")
    endif()
    set(litheview_embedded_budget_arguments
        -DSIZE_TOOL=${CMAKE_SIZE}
        -DCORE_ARCHIVE=$<TARGET_FILE:litheview_core>
        -DCOMPACT_FONT_ARCHIVE=$<TARGET_FILE:litheview_compact_font>
        -DDEMO_ARCHIVE=$<TARGET_FILE:litheview_demo_common>
        -DSTACK_ROOT=${CMAKE_BINARY_DIR}
        -DMAX_TEXT_BYTES=${litheview_embedded_max_text_bytes}
        -DMAX_DATA_BYTES=256
        -DMAX_BSS_BYTES=256
        -DMAX_STACK_FRAME_BYTES=${LITHEVIEW_EMBEDDED_STACK_FRAME_LIMIT}
        -DMETRICS_FILE=${CMAKE_BINARY_DIR}/litheview_embedded_metrics.json)
    set(litheview_embedded_budget_dependencies
        litheview_core
        litheview_demo_common)
    if(litheview_resolved_demo_font_backend STREQUAL "GENERATED_SANS")
        list(APPEND litheview_embedded_budget_arguments
            -DFONTS_ARCHIVE=$<TARGET_FILE:litheview_generated_fonts>)
        list(APPEND litheview_embedded_budget_dependencies
            litheview_generated_fonts)
    endif()
    if(TARGET litheview_stm32u545_probe)
        list(APPEND litheview_embedded_budget_arguments
            -DPROBE_ARCHIVE=$<TARGET_FILE:litheview_stm32u545_probe>)
        list(APPEND litheview_embedded_budget_dependencies
            litheview_stm32u545_probe)
    endif()
    add_custom_target(litheview_embedded_budget ALL
        COMMAND ${CMAKE_COMMAND}
            ${litheview_embedded_budget_arguments}
            -P ${CMAKE_CURRENT_SOURCE_DIR}/tools/check_embedded_budget.cmake
        DEPENDS ${litheview_embedded_budget_dependencies}
        VERBATIM)

    endif()

    # The specialized acceptance profiles always use the compact font. Their
    # ceilings are separate so a high-fidelity demo resource cannot silently
    # consume the unused headroom of an unrelated profile.
    set(litheview_round_profile_max_text_bytes 60000)
    # All profiles include the same Core event implementation in their archive sum.
    foreach(profile_budget IN ITEMS litheview_round_profile_max_text_bytes)
        math(EXPR ${profile_budget}
            "${${profile_budget}} + ${litheview_control_events_text_allowance} + ${litheview_drop_down_text_allowance}")
    endforeach()

    if(TARGET litheview_stm32u545_round_probe)
        add_custom_target(litheview_embedded_round_budget ALL
            COMMAND ${CMAKE_COMMAND}
                -DSIZE_TOOL=${CMAKE_SIZE}
                -DCORE_ARCHIVE=$<TARGET_FILE:litheview_core>
                -DCOMPACT_FONT_ARCHIVE=$<TARGET_FILE:litheview_compact_font>
                -DDEMO_ARCHIVE=$<TARGET_FILE:litheview_round_demo>
                -DPROBE_ARCHIVE=$<TARGET_FILE:litheview_stm32u545_round_probe>
                -DSTACK_ROOT=${CMAKE_BINARY_DIR}
                -DMAX_TEXT_BYTES=${litheview_round_profile_max_text_bytes}
                -DMAX_DATA_BYTES=256
                -DMAX_BSS_BYTES=256
                -DMAX_STACK_FRAME_BYTES=${LITHEVIEW_EMBEDDED_STACK_FRAME_LIMIT}
                -DMETRICS_FILE=${CMAKE_BINARY_DIR}/litheview_embedded_round_metrics.json
                -P ${CMAKE_CURRENT_SOURCE_DIR}/tools/check_embedded_budget.cmake
            DEPENDS
                litheview_core
                litheview_round_demo
                litheview_stm32u545_round_probe
            VERBATIM)
    endif()

endif()

install(FILES LICENSE NOTICE.md THIRD_PARTY_NOTICES.md
    DESTINATION ${CMAKE_INSTALL_DATADIR}/LitheView)
install(DIRECTORY licenses/ DESTINATION ${CMAKE_INSTALL_DATADIR}/LitheView/licenses)

install(TARGETS litheview_core litheview_segments
    EXPORT LitheViewTargets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)
install(DIRECTORY include/ DESTINATION ${CMAKE_INSTALL_INCLUDEDIR})
install(FILES
    ${CMAKE_CURRENT_BINARY_DIR}/generated/lithe/render_config.hpp
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/lithe)
install(EXPORT LitheViewTargets
    FILE LitheViewTargets.cmake
    NAMESPACE LitheView::
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/LitheView
)
configure_package_config_file(
    cmake/LitheViewConfig.cmake.in
    ${CMAKE_CURRENT_BINARY_DIR}/LitheViewConfig.cmake
    INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/LitheView
)
write_basic_package_version_file(
    ${CMAKE_CURRENT_BINARY_DIR}/LitheViewConfigVersion.cmake
    VERSION ${LITHEVIEW_VERSION}
    COMPATIBILITY SameMajorVersion
)
install(FILES
    ${CMAKE_CURRENT_BINARY_DIR}/LitheViewConfig.cmake
    ${CMAKE_CURRENT_BINARY_DIR}/LitheViewConfigVersion.cmake
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/LitheView
)
