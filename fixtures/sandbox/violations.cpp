#include "turing_sdk.hpp"
#include <unistd.h>
#include <sys/socket.h>
#include <pthread.h>
#include <fstream>
#include <vector>
#include <ctime>
#include <cstdlib>
#ifndef VIOLATION
#define VIOLATION 0
#endif
static void* thread_main(void*) { return nullptr; }
int main() {
    tsr::Client client;
    const int violation = client.initialization().value("testViolation", VIOLATION);
    while (client.getframeinfo()) {
        if (violation == 0) { std::ifstream secret("/etc/shadow"); }
        if (violation == 1) { socket(AF_INET, SOCK_STREAM, 0); }
        if (violation == 2) { fork(); }
        if (violation == 3) { pthread_t thread; pthread_create(&thread, nullptr, thread_main, nullptr); }
        if (violation == 4) { for (;;) {} }
        if (violation == 5) {
            for (;;) {
                volatile char* memory = static_cast<char*>(std::malloc(4 * 1024 * 1024));
                if (memory == nullptr) _exit(86);
                for (int offset = 0; offset < 4 * 1024 * 1024; offset += 4096) memory[offset] = 'm';
            }
        }
        if (violation == 6) { std::cout << std::string(70000, 'x') << std::endl; }
        if (violation == 7) {
            const auto started = std::clock();
            volatile unsigned value = 1;
            while (double(std::clock() - started) / CLOCKS_PER_SEC < 0.025) value = value * 1664525u + 1013904223u;
        }
        if (violation == 8) { std::cout << "{" << std::endl; continue; }
        if (violation == 9 || violation == 10) {
            tsr::json actions = tsr::json::array();
            if (violation == 10)
                for (int index = 0; index < 129; index++) actions.push_back({{"unitId", client.side() * 100}, {"action", "wait"}, {"ticks", 1}});
            std::cout << tsr::json({{"type", "relay-commands"}, {"protocolVersion", 2}, {"side", client.side()},
                {"tick", client.frame().at("tick").get<int>() + (violation == 9 ? 1 : 0)}, {"actions", actions}}).dump() << std::endl;
            continue;
        }
        client.submit();
    }
}
