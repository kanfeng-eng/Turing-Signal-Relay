constexpr unsigned calculate(unsigned value) {
    for (unsigned index = 0; index < 200000; ++index) {
        value = (value * 1664525u + 1013904223u) ^ index;
    }
    return value;
}
template<unsigned Count> struct Work {
    static constexpr unsigned value = calculate(Count) + Work<Count - 1>::value;
};
template<> struct Work<0> { static constexpr unsigned value = 1; };
int main() { return Work<500>::value; }
