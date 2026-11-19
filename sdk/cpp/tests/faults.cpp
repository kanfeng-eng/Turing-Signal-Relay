#include "../turing_sdk.hpp"
#include <chrono>
#include <thread>
#include <vector>
#include <ctime>
#ifndef FAULT_MODE
#define FAULT_MODE 0
#endif
int main() {
    try {
        tsr::Client client;
        int frames = 0;
        while (client.getframeinfo()) {
            if (FAULT_MODE == 0) { std::cout << "{bad-json" << std::endl; }
            else if (FAULT_MODE == 1) { std::cout << std::string(70000, 'x') << std::endl; }
            else if (FAULT_MODE == 2) return 7;
            else if (FAULT_MODE == 3) { std::this_thread::sleep_for(std::chrono::seconds(5)); client.submit(); }
            else {
                if (FAULT_MODE == 8) {
                    const auto start = std::clock();
                    volatile unsigned work = 1;
                    while (double(std::clock() - start) / CLOCKS_PER_SEC < 0.025) work = work * 1664525u + 1013904223u;
                }
                if (FAULT_MODE == 4 && frames == 0) std::this_thread::sleep_for(std::chrono::milliseconds(150));
                if (FAULT_MODE == 6 && frames == 0) std::cerr << std::string(2 * 1024 * 1024, 'x') << std::flush;
                if (FAULT_MODE == 7) {
                    std::cout << tsr::json({{"type", "relay-commands"}, {"protocolVersion", 2}, {"side", client.side()},
                        {"tick", client.frame().at("tick").get<int>() + 1}, {"actions", tsr::json::array()}}).dump() << std::endl;
                } else if (FAULT_MODE == 5) {
                    std::vector<char> memory(260 * 1024 * 1024, 'm');
                    client.submit();
                } else client.submit();
            }
            ++frames;
        }
        return 0;
    } catch (...) { return 2; }
}
