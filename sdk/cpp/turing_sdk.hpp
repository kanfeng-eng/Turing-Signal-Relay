#pragma once
#include <iostream>
#include <stdexcept>
#include <string>
#include "third_party/nlohmann/json.hpp"

namespace tsr {
using json = nlohmann::json;

class Client {
    json init_, frame_, actions_ = json::array();
public:
    Client() {
        std::string line;
        if (!std::getline(std::cin, line)) throw std::runtime_error("missing initialization");
        init_ = json::parse(line);
        if (init_.at("type") != "relay-init" || init_.at("protocolVersion") != 2)
            throw std::runtime_error("unsupported protocol");
        std::cout << json({{"type", "relay-ready"}, {"protocolVersion", 2}, {"side", side()}}).dump() << std::endl;
    }
    int side() const { return init_.at("side").get<int>(); }
    const json& initialization() const { return init_; }
    const json& frame() const { return frame_; }
    bool getframeinfo() {
        std::string line;
        if (!std::getline(std::cin, line)) return false;
        frame_ = json::parse(line);
        if (frame_.at("type") != "relay-frame" || frame_.at("protocolVersion") != 2 || frame_.at("side") != side())
            throw std::runtime_error("invalid frame");
        actions_ = json::array();
        return !frame_.at("completed").get<bool>();
    }
    void command(json action) {
        if (actions_.size() >= 128) throw std::runtime_error("action limit");
        actions_.push_back(std::move(action));
    }
    void moveTo(int uid, int x, int y) { command({{"unitId", uid}, {"action", "moveTo"}, {"x", x}, {"y", y}}); }
    void move(int uid, int direction) {
        static const int dx[] = {-1, 0, 1, 0}, dy[] = {0, -1, 0, 1};
        if (direction < 0 || direction > 3) throw std::runtime_error("invalid direction");
        for (const auto& unit : frame_.at("units")) if (unit.at("id") == uid) {
            moveTo(uid, unit.at("x").get<int>() + dx[direction], unit.at("y").get<int>() + dy[direction]); return;
        }
        throw std::runtime_error("unit not owned");
    }
    void loadMsg(int uid, int messageId) { command({{"unitId", uid}, {"action", "loadMsg"}, {"messageId", messageId}}); }
    void deliverMsg(int uid) { command({{"unitId", uid}, {"action", "deliverMsg"}}); }
    void transferMsg(int uid, int target, int messageId) { command({{"unitId", uid}, {"action", "transferMsg"}, {"targetUnitId", target}, {"messageId", messageId}}); }
    void wait(int uid, int ticks) { command({{"unitId", uid}, {"action", "wait"}, {"ticks", ticks}}); }
    void setupStation(int uid) { command({{"unitId", uid}, {"action", "setupStation"}}); }
    void withdrawStation(int uid) { command({{"unitId", uid}, {"action", "withdrawStation"}}); }
    void suppress(int uid, int x, int y) { command({{"unitId", uid}, {"action", "suppress"}, {"x", x}, {"y", y}}); }
    void buildBridge(int uid, int x, int y) { command({{"unitId", uid}, {"action", "buildBridge"}, {"x", x}, {"y", y}}); }
    void buildRelay(int uid, int x, int y) { command({{"unitId", uid}, {"action", "buildRelay"}, {"x", x}, {"y", y}}); }
    void pickup(int uid, const std::string& type = "materials") {
        if (type != "materials") throw std::runtime_error("unsupported resource type");
        command({{"unitId", uid}, {"action", "pickup"}});
    }
    void unloadMaterials(int uid) { command({{"unitId", uid}, {"action", "unloadMaterials"}}); }
    void deliverMaterials(int uid) { command({{"unitId", uid}, {"action", "deliverMaterials"}}); }
    void transferMaterials(int uid, int target) { command({{"unitId", uid}, {"action", "transferMaterials"}, {"targetUnitId", target}}); }
    void submit() {
        std::cout << json({{"type", "relay-commands"}, {"protocolVersion", 2}, {"side", side()},
            {"tick", frame_.at("tick")}, {"actions", actions_}}).dump() << std::endl;
    }
};
}
