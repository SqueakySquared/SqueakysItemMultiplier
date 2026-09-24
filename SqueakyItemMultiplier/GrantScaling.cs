using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SqueakyItemMultiplier
{
    // Weak keys let inventories disappear when players leave. Reset between runs.
    internal sealed class GrantScaling
    {
        // One table per plugin instance; pickup tracking requires persistent storage.
        private readonly ConditionalWeakTable<object, Dictionary<int, int>> _pickupCounts =
            // ReSharper disable once HeapView.ObjectAllocation.Evident
            new ConditionalWeakTable<object, Dictionary<int, int>>();

        public double GetFactor(object inventory, int itemIndex, int multiplier, bool exponential)
        {
            if (!exponential || multiplier <= 1)
                return multiplier;

            var previousPickups = 0;
            if (_pickupCounts.TryGetValue(inventory, out var counts))
                counts.TryGetValue(itemIndex, out previousPickups);

            return Math.Pow(multiplier, (double)previousPickups + 1);
        }

        public void RecordPickup(object inventory, int itemIndex)
        {
            var counts = _pickupCounts.GetOrCreateValue(inventory);
            counts.TryGetValue(itemIndex, out var previousPickups);
            if (previousPickups < int.MaxValue)
                counts[itemIndex] = previousPickups + 1;
        }

        public void Reset()
        {
            _pickupCounts.Clear();
        }

        public static int ScalePermanent(int originalCount, int currentCount, double factor)
        {
            return (int)Math.Min(originalCount * factor, AvailableRoom(currentCount));
        }

        public static float ScaleTemporary(float originalCount, int currentCount, double factor)
        {
            var limit = Math.Min(originalCount * factor, AvailableRoom(currentCount));
            var result = (float)limit;
            // Casting int.MaxValue to float rounds UP to 2^31. Round down instead.
            if (result > limit)
                result = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(result) - 1);
            return result;
        }

        private static double AvailableRoom(int currentCount)
        {
            return Math.Min(int.MaxValue, Math.Max(0L, (long)int.MaxValue - currentCount));
        }
    }
}
