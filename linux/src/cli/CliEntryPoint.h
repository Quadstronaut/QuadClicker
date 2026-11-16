#pragma once

#include "../models/ClickSession.h"
#include <string>

namespace QuadClicker {

class CliEntryPoint {
public:
    /// Parse argv and run headless. Returns a process exit code:
    ///   0   Success / clean stop
    ///   1   Invalid argument
    ///   2   Runtime error
    ///   130 Ctrl+C / SIGINT
    static int run(int argc, char* argv[]);

    /// Parse and validate argv into a session (no side effects; used by tests).
    /// Returns false and sets \p error on any invalid argument.
    static bool parseArgs(int argc, char* argv[], ClickSession& session, std::string& error);
};

} // namespace QuadClicker
