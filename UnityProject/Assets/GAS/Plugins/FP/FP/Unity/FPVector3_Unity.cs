#if UNITY_2017_1_OR_NEWER
using System;
using System.Runtime.CompilerServices;


namespace Herta
{

    public partial struct FPVector3 : IEquatable<FPVector3>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static explicit operator UnityEngine.Vector3(FPVector3 value) => new UnityEngine.Vector3(value.x.AsFloat, value.y.AsFloat, value.z.AsFloat);
        public static implicit operator UnityEngine.Vector3(FPVector3 value) => new UnityEngine.Vector3(value.x.AsFloat, value.y.AsFloat, value.z.AsFloat);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPVector3(UnityEngine.Vector3 value) => new FPVector3(FP.FromFloat_SAFE(value.x),FP.FromFloat_SAFE(value.y),FP.FromFloat_SAFE(value.z));

    }
}
#endif
