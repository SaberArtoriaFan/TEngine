#if UNITY_2017_1_OR_NEWER
using System;
using System.Runtime.CompilerServices;


namespace Herta
{
    using Unity.Collections;
    
    public partial struct FPVector2 : IEquatable<FPVector2>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static  implicit operator UnityEngine.Vector2(FPVector2 value) => new UnityEngine.Vector2(value.x.AsFloat, value.y.AsFloat);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FPVector2(UnityEngine.Vector2 value) => new FPVector2(FP.FromFloat_SAFE(value.x),FP.FromFloat_SAFE(value.y));

   
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lengthsq(FPVector2 value)
        {
            return FPVector2.Dot(value,value);
        }
        
        
    }
}
#endif
