#pragma once
#include "../turing_sdk.hpp"
#include <cstdlib>

inline int courier_main(int local_unit, bool nearest) {
    try {
        tsr::Client client;
        tsr::json home, report;
        for (const auto& site : client.initialization().at("map").at("sites")) {
            if (site.at("side") != client.side()) continue;
            if (site.at("kind") == "headquarters") home = site;
            if (site.at("kind") == "report" && report.is_null()) report = site;
        }
        if (home.is_null() || report.is_null()) throw std::runtime_error("missing map sites");
        while (client.getframeinfo()) {
            const auto& frame = client.frame();
            const int uid = client.side() * 100 + local_unit;
            const tsr::json* courier = nullptr;
            for (const auto& unit : frame.at("units")) if (unit.at("id") == uid) courier = &unit;
            if (!courier) throw std::runtime_error("missing courier");
            if (courier->at("task") != "idle") { client.submit(); continue; }
            int x = courier->at("x"), y = courier->at("y");
            if (!courier->at("messageIds").empty()) {
                if (x == home.at("x") && y == home.at("y")) client.deliverMsg(uid);
                else client.moveTo(uid, home.at("x"), home.at("y"));
            } else {
                const tsr::json* message = nullptr;
                int distance = 100000;
                for (const auto& item : frame.at("messages")) {
                    if (item.at("kind") != "plain" || item.at("carrierId") != -1 || item.at("deliveredTick").get<int>() >= 0) continue;
                    int current = std::abs(x - item.at("x").get<int>()) + std::abs(y - item.at("y").get<int>());
                    if (!message || (nearest && current < distance)) { message = &item; distance = current; }
                    if (!nearest) break;
                }
                int tx = message ? message->at("x").get<int>() : report.at("x").get<int>();
                int ty = message ? message->at("y").get<int>() : report.at("y").get<int>();
                if (x == tx && y == ty) { if (message) client.loadMsg(uid, message->at("id")); }
                else client.moveTo(uid, tx, ty);
            }
            client.submit();
        }
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << std::endl;
        return 2;
    }
}
