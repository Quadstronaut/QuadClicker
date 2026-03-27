#pragma once

namespace QuadClicker {

class CliEntryPoint {
public:
    /// Parse argv and run headless. Returns a process exit code:
    ///   0   Success / clean stop
    ///   1   Invalid argument
    ///   2   Runtime error
    ///   130 Ctrl+C / SIGINT
    static int run(int argc, char* argv[]);
};

} // namespace QuadClicker
