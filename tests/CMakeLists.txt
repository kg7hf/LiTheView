# SPDX-FileCopyrightText: 2026 Paul R. Decker
# SPDX-License-Identifier: GPL-3.0-only

if(LITHEVIEW_BUILD_RENDERING_TESTS)
    add_executable(litheview_tests
        generated_form_test.cpp
        generated_widget_metadata_test.cpp
        test_main.cpp)
    target_link_libraries(litheview_tests PRIVATE
        litheview_demo_common
        litheview_generated_form_smoke
        litheview_generated_button_form_smoke
        litheview_generated_tab_form_smoke
        litheview_generated_wheel_form_smoke
        litheview_round_demo
        litheview_stm32u545_probe
        litheview_stm32u545_round_probe)
    litheview_configure_first_party_target(litheview_tests)
    target_compile_definitions(litheview_tests PRIVATE
        LITHEVIEW_GENERATED_INCLUDE_SOURCE_XML=1)
    add_test(NAME litheview_tests COMMAND litheview_tests)
endif()
add_executable(litheview_control_objects_tests control_objects_test.cpp)
target_link_libraries(litheview_control_objects_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_control_objects_tests)
add_test(NAME litheview_control_objects_tests COMMAND litheview_control_objects_tests)
add_executable(litheview_control_events_tests control_events_test.cpp)
target_link_libraries(litheview_control_events_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_control_events_tests)
add_test(NAME litheview_control_events_tests COMMAND litheview_control_events_tests)
add_executable(litheview_drop_down_tests drop_down_test.cpp)
target_link_libraries(litheview_drop_down_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_drop_down_tests)
add_test(NAME litheview_drop_down_tests COMMAND litheview_drop_down_tests)
add_executable(litheview_text_events_tests text_events_test.cpp)
target_link_libraries(litheview_text_events_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_text_events_tests)
add_test(NAME litheview_text_events_tests COMMAND litheview_text_events_tests)
add_executable(litheview_text_setpoint_tests text_setpoint_test.cpp)
target_link_libraries(litheview_text_setpoint_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_text_setpoint_tests)
add_test(NAME litheview_text_setpoint_tests COMMAND litheview_text_setpoint_tests)
add_executable(litheview_framebuffer_free_tests framebuffer_free_test.cpp)
target_link_libraries(litheview_framebuffer_free_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_framebuffer_free_tests)
add_test(NAME litheview_framebuffer_free_tests COMMAND litheview_framebuffer_free_tests)
add_executable(litheview_storage_layout_tests storage_layout_test.cpp)
target_link_libraries(litheview_storage_layout_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_storage_layout_tests)
add_test(NAME litheview_storage_layout_tests COMMAND litheview_storage_layout_tests)
add_executable(litheview_r2_render_tests r2_render_test.cpp)
target_link_libraries(litheview_r2_render_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_r2_render_tests)
add_test(NAME litheview_r2_render_tests COMMAND litheview_r2_render_tests)
add_executable(litheview_native_cell_tests native_cell_render_test.cpp)
target_link_libraries(litheview_native_cell_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_native_cell_tests)
add_test(NAME litheview_native_cell_tests COMMAND litheview_native_cell_tests)
add_executable(litheview_native_preview_tests native_preview_test.cpp)
target_link_libraries(litheview_native_preview_tests PRIVATE LitheView::Core LitheView::CompactFont)
target_include_directories(litheview_native_preview_tests PRIVATE ${PROJECT_SOURCE_DIR}/tests/rendering/common)
litheview_configure_first_party_target(litheview_native_preview_tests)
add_test(NAME litheview_native_preview_tests COMMAND litheview_native_preview_tests)
add_executable(litheview_display_definition_tests display_definition_test.cpp)
target_link_libraries(litheview_display_definition_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_display_definition_tests)
add_test(NAME litheview_display_definition_tests COMMAND litheview_display_definition_tests)
add_executable(litheview_surface_input_tests surface_input_test.cpp)
target_link_libraries(litheview_surface_input_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_surface_input_tests)
add_test(NAME litheview_surface_input_tests COMMAND litheview_surface_input_tests)
add_executable(litheview_surface_layout_adapter_tests surface_layout_adapter_test.cpp)
target_link_libraries(litheview_surface_layout_adapter_tests PRIVATE LitheView::Core LitheView::CompactFont)
litheview_configure_first_party_target(litheview_surface_layout_adapter_tests)
add_test(NAME litheview_surface_layout_adapter_tests COMMAND litheview_surface_layout_adapter_tests)
add_executable(litheview_surface_layout_tests surface_layout_test.cpp)
target_include_directories(litheview_surface_layout_tests PRIVATE ${PROJECT_SOURCE_DIR}/include)
target_compile_features(litheview_surface_layout_tests PRIVATE cxx_std_23)
litheview_configure_first_party_target(litheview_surface_layout_tests)
add_test(NAME litheview_surface_layout_tests COMMAND litheview_surface_layout_tests)
if(LITHEVIEW_BUILD_RENDERING_TESTS)
    add_executable(litheview_round_surface_tests round_surface_test.cpp)
    target_link_libraries(litheview_round_surface_tests PRIVATE litheview_round_demo)
    litheview_configure_first_party_target(litheview_round_surface_tests)
    add_test(NAME litheview_round_surface_tests COMMAND litheview_round_surface_tests)
endif()
add_test(NAME litheview_cast_audit
    COMMAND ${CMAKE_COMMAND}
        -DLITHEVIEW_ROOT=${PROJECT_SOURCE_DIR}
        -P ${PROJECT_SOURCE_DIR}/tools/check_casts.cmake)
