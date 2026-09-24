using System;
using SqueakyItemMultiplier;

var scaling = new GrantScaling();
var player = new object();
var otherPlayer = new object();
int checks = 0;

void Equal(double expected, double actual, string scenario)
{
    checks++;
    if (expected != actual)
        throw new Exception($"{scenario}: expected {expected}, got {actual}");
}

foreach (int expected in new[] { 5, 25, 125 })
{
    double factor = scaling.GetFactor(player, 1, 5, true);
    Equal(expected, GrantScaling.ScalePermanent(1, 0, factor), "successive pickups");
    Equal(expected * 2, GrantScaling.ScalePermanent(2, 0, factor), "batch grant");
    scaling.RecordPickup(player, 1);
}
Equal(5, scaling.GetFactor(player, 2, 5, true), "different item starts at base multiplier");
Equal(5, scaling.GetFactor(otherPlayer, 1, 5, true), "different player starts at base multiplier");
Equal(5, scaling.GetFactor(player, 1, 5, false), "flat mode ignores history");
Equal(625, scaling.GetFactor(player, 1, 5, true), "flat mode does not reset history");
Equal(16, scaling.GetFactor(player, 1, 2, true), "live multiplier change uses current base");
Equal(1, scaling.GetFactor(player, 1, 1, true), "multiplier one");
scaling.Reset();
Equal(5, scaling.GetFactor(player, 1, 5, true), "new run resets history");

Equal(15, GrantScaling.ScalePermanent(3, 7, 5), "flat batch grant");
Equal(int.MaxValue, GrantScaling.ScalePermanent(int.MaxValue, 0, int.MaxValue), "large product");
Equal(7, GrantScaling.ScalePermanent(2, int.MaxValue - 7, 125), "remaining capacity");
Equal(0, GrantScaling.ScalePermanent(1, int.MaxValue, 5), "full inventory");
Equal(int.MaxValue, GrantScaling.ScalePermanent(1, 0, double.PositiveInfinity), "exponent overflow saturates");
Equal(1.25, GrantScaling.ScaleTemporary(0.25f, 0, 5), "fractional temporary items");
Equal(0, GrantScaling.ScaleTemporary(1, int.MaxValue, 5), "full temporary inventory");
Equal(2147483520d, GrantScaling.ScaleTemporary(1, 0, double.PositiveInfinity), "float rounds below integer limit");

// Cover float rounding boundaries and capacity near the integer limit.
foreach (int current in new[] { 0, 1, 63, 127, 128, 1000000, int.MaxValue - 257, int.MaxValue - 1, int.MaxValue })
foreach (double factor in new[] { 5d, 125d, int.MaxValue, double.PositiveInfinity })
{
    float result = GrantScaling.ScaleTemporary(1, current, factor);
    checks++;
    if (result < 0 || !float.IsFinite(result) || (double)current + result > int.MaxValue)
        throw new Exception($"Temporary grant exceeds capacity: current={current}, factor={factor}, result={result}");
}

for (int i = 0; i < 2000; i++)
    scaling.RecordPickup(player, 1);
Equal(int.MaxValue, GrantScaling.ScalePermanent(1, 0, scaling.GetFactor(player, 1, 5, true)),
    "long exponential sequence saturates");

Console.WriteLine($"Passed {checks} grant scaling checks.");
