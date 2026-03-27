find_package(Qt6 REQUIRED COMPONENTS Test)

# Helper to add a Qt Test executable
function(add_qt_test TEST_NAME SOURCE_FILE)
    add_executable(${TEST_NAME} ${SOURCE_FILE})

    target_link_libraries(${TEST_NAME}
        PRIVATE
            quadclicker_core
            Qt6::Test
    )

    target_include_directories(${TEST_NAME}
        PRIVATE
            ${CMAKE_SOURCE_DIR}/src
    )

    # Register with CTest
    add_test(NAME ${TEST_NAME} COMMAND ${TEST_NAME})
endfunction()

add_qt_test(ClickRateParserTests ClickRateParserTests.cpp)
add_qt_test(ClickSessionTests    ClickSessionTests.cpp)
add_qt_test(CliArgumentTests     CliArgumentTests.cpp)
