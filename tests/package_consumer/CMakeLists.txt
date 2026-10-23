# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

cmake_minimum_required(VERSION 3.25)
project(LitheViewPackageConsumer LANGUAGES CXX)

set(LITHEVIEW_SOURCE "" CACHE PATH "Optional source checkout for subdirectory integration")
if(LITHEVIEW_SOURCE)
    foreach(option IN ITEMS BUILD_RENDERING_FIXTURES BUILD_RENDERING_TESTS BUILD_WIN32
            BUILD_STM32U545_PROBE BUILD_TESTS BUILD_DESIGNER_PREVIEW BUILD_COMPACT_FONT BUILD_GENERATED_FONTS
            BUILD_GENERATED_SANS20 BUILD_GENERATED_SANS28)
        set(LITHEVIEW_${option} OFF CACHE BOOL "Core-only consumer" FORCE)
    endforeach()
    add_subdirectory("${LITHEVIEW_SOURCE}" litheview)
else()
    find_package(LitheView CONFIG REQUIRED)
endif()
if(TARGET LitheView::Demo)
    message(FATAL_ERROR "The public library package must not export an example application")
endif()

add_executable(litheview_package_consumer main.cpp)
target_compile_features(litheview_package_consumer PRIVATE cxx_std_23)
target_link_libraries(litheview_package_consumer PRIVATE
    LitheView::Core)

add_executable(litheview_framebuffer_free_consumer framebuffer_free_main.cpp)
target_link_libraries(litheview_framebuffer_free_consumer PRIVATE LitheView::Core)

enable_testing()
add_test(NAME litheview_package_consumer COMMAND litheview_package_consumer)
add_test(NAME litheview_framebuffer_free_consumer COMMAND litheview_framebuffer_free_consumer)
if(TARGET LitheView::GeneratedFonts)
add_executable(litheview_generated_fonts_consumer generated_fonts_main.cpp)
target_compile_features(litheview_generated_fonts_consumer PRIVATE cxx_std_23)
target_link_libraries(litheview_generated_fonts_consumer PRIVATE
    LitheView::GeneratedFonts)

add_test(NAME litheview_generated_fonts_consumer COMMAND litheview_generated_fonts_consumer)
endif()
if(TARGET LitheView::GeneratedSans20 AND TARGET LitheView::GeneratedSans28)
    add_executable(litheview_font_sizes_consumer ../font_sizes_test.cpp)
    target_compile_features(litheview_font_sizes_consumer PRIVATE cxx_std_23)
    target_link_libraries(litheview_font_sizes_consumer PRIVATE
        LitheView::GeneratedSans20 LitheView::GeneratedSans28)
    add_test(NAME litheview_font_sizes_consumer COMMAND litheview_font_sizes_consumer)
endif()

add_executable(litheview_owned_font_consumer owned_font_main.cpp)
target_link_libraries(litheview_owned_font_consumer PRIVATE LitheView::Core)
add_test(NAME litheview_owned_font_consumer COMMAND litheview_owned_font_consumer)
