

namespace MCBurst
{
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public unsafe static class Methods
    {
        public static int Index3D1D(int3 i, int3 s) => i.z * (s.x * s.y) + i.y * s.x + i.x;
        public static int3 Index1D3D(int i, int3 s) => new int3(i % s.x, (i / s.x) % s.y, i / (s.x * s.y));

        
        public static U[] CastArray<T, U>(this NativeList<T> list) where U : struct where T : struct
        {
            // enforce size alignment 
            var tSize = UnsafeUtility.SizeOf<T>();
            var uSize = UnsafeUtility.SizeOf<U>();
            var byteLen = ((long)list.Length) * tSize;
            var uLen = byteLen / uSize;
            if (uLen * uSize != byteLen) throw new System.InvalidOperationException($"Types {typeof(T)} (array length {list.Length}) and " +
                $"{typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            // ... 
            var Ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<T>(list);
            var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(Ptr, (int)uLen, Allocator.None);
            return result.ToArray();
        }
    }
}