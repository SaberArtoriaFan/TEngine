using System;

namespace Saber.GAS.Authoring
{
    internal static class CombatAuthoringArrayUtility
    {
        public static bool AppendUnique<T>(ref T[] values, T value)
            where T : class
        {
            if (value == null)
            {
                return false;
            }

            if (values == null)
            {
                values = Array.Empty<T>();
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (ReferenceEquals(values[index], value))
                {
                    return false;
                }
            }

            Array.Resize(ref values, values.Length + 1);
            values[^1] = value;
            return true;
        }

        public static bool RemoveReference<T>(ref T[] values, T value)
            where T : class
        {
            if (value == null || values == null || values.Length == 0)
            {
                return false;
            }

            var index = Array.IndexOf(values, value);
            if (index < 0)
            {
                return false;
            }

            var next = new T[values.Length - 1];
            if (index > 0)
            {
                Array.Copy(values, 0, next, 0, index);
            }

            if (index < values.Length - 1)
            {
                Array.Copy(values, index + 1, next, index, values.Length - index - 1);
            }

            values = next;
            return true;
        }
    }
}
