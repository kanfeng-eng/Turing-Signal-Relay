#include "turing_sdk.hpp"
#include <unistd.h>
#include <sys/socket.h>
#include <pthread.h>
#include <fstream>
#include <vector>
#include <ctime>
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
        if (violation == 5) { std::vector<char> memory(300 * 1024 * 1024, 'm'); std::cout << memory.front(); }
        if (violation == 6) { std::cout << std::string(70000, 'x') << std::endl; }
        if (violation == 7) {
            const auto started = std::clock();
            volatile unsigned value = 1;
            while (double(std::clock() - started) / CLOCKS_PER_SEC < 0.025) value = value * 1664525u + 1013904223u;
        }
        client.submit();
    }
}
