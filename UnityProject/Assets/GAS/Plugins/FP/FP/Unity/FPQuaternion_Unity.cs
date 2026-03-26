#if UNITY_2017_1_OR_NEWER
using System;
using System.Runtime.CompilerServices;

namespace Herta
{

    partial struct FPQuaternion : IEquatable<FPQuaternion>
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator UnityEngine.Quaternion(FPQuaternion value) =>
            new UnityEngine.Quaternion(value.X.AsFloat, value.Y.AsFloat, value.Z.AsFloat, value.W.AsFloat);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPQuaternion(UnityEngine.Quaternion value) =>
            new FPQuaternion(FP.FromFloat_SAFE(value.x), FP.FromFloat_SAFE(value.y), FP.FromFloat_SAFE(value.z), FP.FromFloat_SAFE(value.w));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(FPQuaternion other) =>
            X == other.X && Y == other.Y && Z == other.Z && W == other.W;

        public override bool Equals(object obj) =>
            obj is FPQuaternion other && Equals(other);
    }
}
#endif