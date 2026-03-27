cmake_minimum_required(VERSION 3.20)

project(QuadClicker
    VERSION 1.0.0
    DESCRIPTION "Open-source native auto-clicker"
    HOMEPAGE_URL "https://github.com/quadstronaut/quadclicker"
    LANGUAGES CXX
)

# ── C++ Standard ──────────────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ── Qt6 ───────────────────────────────────────────────────────────────────────
# Allow the user to hint where Qt6 lives:  cmake -DCMAKE_PREFIX_PATH=/path/to/Qt6
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)

find_package(Qt6 6.2 REQUIRED COMPONENTS
    Core
    Gui
    Widgets
    Concurrent
    DBus
)

# Optional (only needed for the tray icon theme fallback)
find_package(Qt6 6.2 QUIET COMPONENTS Svg)

# ── X11 / XTest / XScreenSaver ────────────────────────────────────────────────
find_package(X11 REQUIRED)

# XTest
if(NOT X11_XTest_FOUND)
    message(FATAL_ERROR
        "libXtst (XTest extension) not found.\n"
        "Install: sudo apt install libxtst-dev   (Debian/Ubuntu)\n"
        "         sudo dnf install libXtst-devel (Fedora)")
endif()

# XScreenSaver
if(NOT X11_Xscreensaver_FOUND)
    message(FATAL_ERROR
        "libXss (XScreenSaver extension) not found.\n"
        "Install: sudo apt install libxss-dev   (Debian/Ubuntu)\n"
        "         sudo dnf install libXScrnSaver-devel (Fedora)")
endif()

# ── Source files ──────────────────────────────────────────────────────────────
set(CORE_SOURCES
    src/core/ClickEngine.cpp
    src/core/ClickEngine.h
    src/core/ClickRateParser.cpp
    src/core/ClickRateParser.h
    src/core/IdleDetector.cpp
    src/core/IdleDetector.h
    src/core/InputInjector.h
    src/core/InputInjectorFactory.cpp
    src/core/InputInjectorFactory.h
    src/core/InputInjectorX11.cpp
    src/core/InputInjectorWayland.cpp
    src/core/HotkeyManager.cpp
    src/core/HotkeyManager.h
    src/core/LocationPicker.cpp
    src/core/LocationPicker.h
    src/core/TrayManager.cpp
    src/core/TrayManager.h
)

set(MODEL_SOURCES
    src/models/AppSettings.cpp
    src/models/AppSettings.h
    src/models/ClickSession.h
    src/models/MouseButton.h
    src/models/ClickType.h
)

set(CLI_SOURCES
    src/cli/CliEntryPoint.cpp
    src/cli/CliEntryPoint.h
)

set(GUI_SOURCES
    src/MainWindow.cpp
    src/MainWindow.h
    src/main.cpp
)

# ── Static library for core logic (shared between app and tests) ───────────────
add_library(quadclicker_core STATIC
    ${CORE_SOURCES}
    ${MODEL_SOURCES}
    ${CLI_SOURCES}
)

target_include_directories(quadclicker_core
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${X11_INCLUDE_DIR}
)

target_link_libraries(quadclicker_core
    PUBLIC
        Qt6::Core
        Qt6::Gui
        Qt6::Widgets
        Qt6::Concurrent
        Qt6::DBus
        ${X11_LIBRARIES}
        ${X11_XTest_LIB}
        ${X11_Xscreensaver_LIB}
)

# uinput is a kernel interface — no separate library needed, but we need the kernel headers.
# QT_DBUS_LIB is automatically defined by Qt when Qt6::DBus is linked, which enables the
# Wayland idle detection path in IdleDetector.cpp.

# ── Main executable ────────────────────────────────────────────────────────────
add_executable(quadclicker ${GUI_SOURCES})

target_link_libraries(quadclicker
    PRIVATE
        quadclicker_core
        Qt6::Widgets
        Qt6::Concurrent
)

target_include_directories(quadclicker
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# ── Compiler warnings ─────────────────────────────────────────────────────────
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(quadclicker_core PRIVATE
        -Wall -Wextra -Wpedantic -Wno-unused-parameter
    )
    target_compile_options(quadclicker PRIVATE
        -Wall -Wextra -Wpedantic -Wno-unused-parameter
    )
endif()

# ── Install ───────────────────────────────────────────────────────────────────
include(GNUInstallDirs)

install(TARGETS quadclicker
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)

install(FILES assets/quadclicker.desktop
    DESTINATION ${CMAKE_INSTALL_DATADIR}/applications
)

install(FILES assets/quadclicker.appdata.xml
    DESTINATION ${CMAKE_INSTALL_DATADIR}/metainfo
)

# ── AppImage (optional) ───────────────────────────────────────────────────────
# To build an AppImage:
#   1. Install linuxdeploy: https://github.com/linuxdeploy/linuxdeploy
#   2. Run: cmake --build build --target appimage
find_program(LINUXDEPLOY linuxdeploy QUIET)
if(LINUXDEPLOY)
    add_custom_target(appimage
        COMMAND ${CMAKE_COMMAND} -E make_directory AppDir/usr/bin
        COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:quadclicker> AppDir/usr/bin/
        COMMAND ${CMAKE_COMMAND} -E copy ${CMAKE_CURRENT_SOURCE_DIR}/assets/quadclicker.desktop AppDir/
        COMMAND ${LINUXDEPLOY}
            --appdir AppDir
            --desktop-file AppDir/quadclicker.desktop
            --output appimage
        WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
        DEPENDS quadclicker
        COMMENT "Building AppImage..."
    )
endif()

# ── Tests ─────────────────────────────────────────────────────────────────────
enable_testing()
add_subdirectory(tests)
