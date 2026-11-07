constexpr long double calculate(unsigned seed) {
    long double value = 0;
    for (unsigned index = 0; index < 100000; ++index) {
        value += __builtin_sinl(1e4000L + (seed * 0.000001L + index) * 1e3990L);
    }
    return value;
}
template<unsigned Count> struct Work {
    static constexpr long double value = calculate(Count) + Work<Count - 1>::value;
};
template<> struct Work<0> { static constexpr long double value = 1; };
int main() { return static_cast<int>(Work<16>::value); }
